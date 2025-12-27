using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立 SnapSplit 核心資料表
/// - bills: 帳單
/// - members: 成員
/// - expenses: 支出
/// </summary>
[Migration(202512270004)]
public class CreateSnapSplitTables : FluentMigrator.Migration
{
    public override void Up()
    {
        // Bills table
        Create.Table("bills").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("owner_id").AsGuid().Nullable()
            .WithColumn("share_code").AsString(20).Nullable()
            .WithColumn("created_at").AsDateTime2().NotNullable()
            .WithColumn("updated_at").AsDateTime2().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable()
            .WithColumn("is_deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("deleted_at").AsDateTime2().Nullable()
            .WithColumn("deleted_by").AsGuid().Nullable();

        Create.Index("ix_bills_owner_id")
            .OnTable("bills").InSchema("snapsplit")
            .OnColumn("owner_id").Ascending();

        // Partial unique index for PostgreSQL (using raw SQL since FluentMigrator doesn't support Filter)
        Execute.Sql(@"
            CREATE UNIQUE INDEX ix_bills_share_code
            ON snapsplit.bills (share_code)
            WHERE share_code IS NOT NULL AND is_deleted = false;
        ");

        // Members table
        Create.Table("members").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("name").AsString(50).NotNullable()
            .WithColumn("display_order").AsInt32().NotNullable()
            .WithColumn("linked_user_id").AsGuid().Nullable();

        Create.ForeignKey("fk_members_bill_id")
            .FromTable("members").InSchema("snapsplit").ForeignColumn("bill_id")
            .ToTable("bills").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.Index("ix_members_bill_order")
            .OnTable("members").InSchema("snapsplit")
            .OnColumn("bill_id").Ascending()
            .OnColumn("display_order").Ascending();

        // Expenses table
        Create.Table("expenses").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("amount").AsDecimal(18, 2).NotNullable()
            .WithColumn("service_fee_percent").AsDecimal(5, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("is_itemized").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("paid_by_id").AsGuid().Nullable()
            .WithColumn("created_at").AsDateTime2().NotNullable()
            .WithColumn("updated_at").AsDateTime2().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable();

        Create.ForeignKey("fk_expenses_bill_id")
            .FromTable("expenses").InSchema("snapsplit").ForeignColumn("bill_id")
            .ToTable("bills").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.ForeignKey("fk_expenses_paid_by_id")
            .FromTable("expenses").InSchema("snapsplit").ForeignColumn("paid_by_id")
            .ToTable("members").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.SetNull);
    }

    public override void Down()
    {
        Delete.Table("expenses").InSchema("snapsplit");
        Delete.Table("members").InSchema("snapsplit");
        Delete.Table("bills").InSchema("snapsplit");
    }
}
