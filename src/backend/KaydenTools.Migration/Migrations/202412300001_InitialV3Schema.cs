using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

[Migration(202412300001)]
public class InitialV3Schema : FluentMigrator.Migration
{
    public override void Up()
    {
        // Enable PostgreSQL extensions
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

        // Create snapsplit schema
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS snapsplit;");

        // -----------------------------------------------------------------------------------------
        // 1. Shared Tables (Users, Auth) - in public schema
        // -----------------------------------------------------------------------------------------

        Create.Table("users")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("email").AsString(255).Nullable().Unique()
            .WithColumn("display_name").AsString(100).Nullable()
            .WithColumn("avatar_url").AsString(500).Nullable()
            // Auth Provider IDs
            .WithColumn("primary_provider").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("line_user_id").AsString(100).Nullable().Unique()
            .WithColumn("line_picture_url").AsString(500).Nullable()
            .WithColumn("google_user_id").AsString(255).Nullable().Unique()
            .WithColumn("google_picture_url").AsString(500).Nullable()
            // Audit
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("updated_at").AsDateTimeOffset().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable()
            // Soft Delete
            .WithColumn("is_deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("deleted_at").AsDateTimeOffset().Nullable()
            .WithColumn("deleted_by").AsGuid().Nullable();

        Create.Table("refresh_tokens")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("user_id").AsGuid().NotNullable().ForeignKey("users", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("token").AsString(255).NotNullable().Unique()
            .WithColumn("device_info").AsString(255).Nullable()
            .WithColumn("ip_address").AsString(45).Nullable()
            .WithColumn("expires_at").AsDateTimeOffset().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("revoked_at").AsDateTimeOffset().Nullable();

        // -----------------------------------------------------------------------------------------
        // 2. SnapSplit Tables (V3 Architecture) - in snapsplit schema
        // -----------------------------------------------------------------------------------------

        // Bills (Root) - create table without foreign keys first
        Create.Table("bills").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("owner_id").AsGuid().Nullable()
            .WithColumn("share_code").AsString(20).Nullable().Unique()
            .WithColumn("version").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("compacted_at_version").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("is_settled").AsBoolean().NotNullable().WithDefaultValue(false)
            // Audit
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("updated_at").AsDateTimeOffset().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable()
            // Soft Delete
            .WithColumn("is_deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("deleted_at").AsDateTimeOffset().Nullable()
            .WithColumn("deleted_by").AsGuid().Nullable();

        // Add foreign key to users (cross-schema)
        Execute.Sql(@"
            ALTER TABLE snapsplit.bills
            ADD CONSTRAINT fk_bills_owner
            FOREIGN KEY (owner_id) REFERENCES public.users(id);
        ");

        Create.Index("idx_bills_share_code").OnTable("bills").InSchema("snapsplit").OnColumn("share_code");
        Create.Index("idx_bills_owner").OnTable("bills").InSchema("snapsplit").OnColumn("owner_id");

        // Operations (V3 Core)
        Create.Table("operations").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("version").AsInt64().NotNullable()
            .WithColumn("op_type").AsString(50).NotNullable()
            .WithColumn("target_id").AsGuid().Nullable()
            .WithColumn("payload").AsCustom("jsonb").NotNullable()
            .WithColumn("created_by_user_id").AsGuid().Nullable()
            .WithColumn("client_id").AsString(100).NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);

        Execute.Sql(@"
            ALTER TABLE snapsplit.operations
            ADD CONSTRAINT fk_operations_bill
            FOREIGN KEY (bill_id) REFERENCES snapsplit.bills(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.operations
            ADD CONSTRAINT fk_operations_user
            FOREIGN KEY (created_by_user_id) REFERENCES public.users(id);
        ");
        Execute.Sql("ALTER TABLE snapsplit.operations ADD CONSTRAINT uq_operations_bill_version UNIQUE (bill_id, version);");

        // -----------------------------------------------------------------------------------------
        // 3. SnapSplit Snapshots (Read Models)
        // -----------------------------------------------------------------------------------------

        // Members
        Create.Table("members").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("display_order").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("linked_user_id").AsGuid().Nullable()
            .WithColumn("original_name").AsString(100).Nullable()
            .WithColumn("claimed_at").AsDateTimeOffset().Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("updated_at").AsDateTimeOffset().Nullable();

        Execute.Sql(@"
            ALTER TABLE snapsplit.members
            ADD CONSTRAINT fk_members_bill
            FOREIGN KEY (bill_id) REFERENCES snapsplit.bills(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.members
            ADD CONSTRAINT fk_members_user
            FOREIGN KEY (linked_user_id) REFERENCES public.users(id);
        ");

        // Expenses
        Create.Table("expenses").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("service_fee_percent").AsDecimal(5, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("is_itemized").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("paid_by_id").AsGuid().Nullable()
            .WithColumn("display_order").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("updated_at").AsDateTimeOffset().Nullable();

        Execute.Sql(@"
            ALTER TABLE snapsplit.expenses
            ADD CONSTRAINT fk_expenses_bill
            FOREIGN KEY (bill_id) REFERENCES snapsplit.bills(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.expenses
            ADD CONSTRAINT fk_expenses_paidby
            FOREIGN KEY (paid_by_id) REFERENCES snapsplit.members(id);
        ");

        // Expense Items
        Create.Table("expense_items").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("expense_id").AsGuid().NotNullable()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("paid_by_id").AsGuid().Nullable()
            .WithColumn("display_order").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);

        Execute.Sql(@"
            ALTER TABLE snapsplit.expense_items
            ADD CONSTRAINT fk_expense_items_expense
            FOREIGN KEY (expense_id) REFERENCES snapsplit.expenses(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.expense_items
            ADD CONSTRAINT fk_expense_items_paidby
            FOREIGN KEY (paid_by_id) REFERENCES snapsplit.members(id);
        ");

        // Expense Participants (Many-to-Many)
        Create.Table("expense_participants").InSchema("snapsplit")
            .WithColumn("expense_id").AsGuid().NotNullable()
            .WithColumn("member_id").AsGuid().NotNullable();

        Execute.Sql("ALTER TABLE snapsplit.expense_participants ADD CONSTRAINT pk_expense_participants PRIMARY KEY (expense_id, member_id);");
        Execute.Sql(@"
            ALTER TABLE snapsplit.expense_participants
            ADD CONSTRAINT fk_expense_participants_expense
            FOREIGN KEY (expense_id) REFERENCES snapsplit.expenses(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.expense_participants
            ADD CONSTRAINT fk_expense_participants_member
            FOREIGN KEY (member_id) REFERENCES snapsplit.members(id) ON DELETE CASCADE;
        ");

        // Expense Item Participants (Many-to-Many)
        Create.Table("expense_item_participants").InSchema("snapsplit")
            .WithColumn("item_id").AsGuid().NotNullable()
            .WithColumn("member_id").AsGuid().NotNullable();

        Execute.Sql("ALTER TABLE snapsplit.expense_item_participants ADD CONSTRAINT pk_expense_item_participants PRIMARY KEY (item_id, member_id);");
        Execute.Sql(@"
            ALTER TABLE snapsplit.expense_item_participants
            ADD CONSTRAINT fk_expense_item_participants_item
            FOREIGN KEY (item_id) REFERENCES snapsplit.expense_items(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.expense_item_participants
            ADD CONSTRAINT fk_expense_item_participants_member
            FOREIGN KEY (member_id) REFERENCES snapsplit.members(id) ON DELETE CASCADE;
        ");

        // Settled Transfers (已結清轉帳)
        Create.Table("settled_transfers").InSchema("snapsplit")
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("from_member_id").AsGuid().NotNullable()
            .WithColumn("to_member_id").AsGuid().NotNullable()
            .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("settled_at").AsDateTimeOffset().NotNullable();

        // Composite Primary Key
        Execute.Sql("ALTER TABLE snapsplit.settled_transfers ADD CONSTRAINT pk_settled_transfers PRIMARY KEY (bill_id, from_member_id, to_member_id);");

        Execute.Sql(@"
            ALTER TABLE snapsplit.settled_transfers
            ADD CONSTRAINT fk_settled_transfers_bill
            FOREIGN KEY (bill_id) REFERENCES snapsplit.bills(id) ON DELETE CASCADE;
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.settled_transfers
            ADD CONSTRAINT fk_settled_transfers_from
            FOREIGN KEY (from_member_id) REFERENCES snapsplit.members(id);
        ");
        Execute.Sql(@"
            ALTER TABLE snapsplit.settled_transfers
            ADD CONSTRAINT fk_settled_transfers_to
            FOREIGN KEY (to_member_id) REFERENCES snapsplit.members(id);
        ");

        // -----------------------------------------------------------------------------------------
        // 4. UrlShortener Tables - in public schema
        // -----------------------------------------------------------------------------------------

        Create.Table("short_urls")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("original_url").AsString().NotNullable()
            .WithColumn("short_code").AsString(20).NotNullable().Unique()
            .WithColumn("owner_id").AsGuid().Nullable()
            .WithColumn("expires_at").AsDateTimeOffset().Nullable()
            .WithColumn("click_count").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
            // Audit
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("updated_at").AsDateTimeOffset().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable()
            // Soft Delete
            .WithColumn("is_deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("deleted_at").AsDateTimeOffset().Nullable()
            .WithColumn("deleted_by").AsGuid().Nullable();

        Create.Index("idx_short_urls_code").OnTable("short_urls").OnColumn("short_code");

        Create.Table("url_clicks")
            .WithColumn("id").AsGuid().PrimaryKey().WithDefault(SystemMethods.NewGuid)
            .WithColumn("short_url_id").AsGuid().NotNullable().ForeignKey("short_urls", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("clicked_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("ip_address").AsString(45).Nullable()
            .WithColumn("user_agent").AsString(500).Nullable()
            .WithColumn("referrer").AsString(500).Nullable()
            .WithColumn("device_type").AsString(50).Nullable();

        Create.Index("idx_url_clicks_url_time").OnTable("url_clicks").OnColumn("short_url_id").Ascending().OnColumn("clicked_at").Descending();
    }

    public override void Down()
    {
        // Drop in reverse order of dependencies
        Delete.Table("url_clicks");
        Delete.Table("short_urls");

        Delete.Table("settled_transfers").InSchema("snapsplit");
        Delete.Table("expense_item_participants").InSchema("snapsplit");
        Delete.Table("expense_participants").InSchema("snapsplit");
        Delete.Table("expense_items").InSchema("snapsplit");
        Delete.Table("expenses").InSchema("snapsplit");
        Delete.Table("members").InSchema("snapsplit");

        Delete.Table("operations").InSchema("snapsplit");
        Delete.Table("bills").InSchema("snapsplit");

        Execute.Sql("DROP SCHEMA IF EXISTS snapsplit;");

        Delete.Table("refresh_tokens");
        Delete.Table("users");
    }
}
