using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立支出細項相關資料表
/// - expense_items: 支出細項（逐項分帳用）
/// - expense_participants: 支出參與者（多對多）
/// - expense_item_participants: 細項參與者（多對多）
/// </summary>
[Migration(202512270005)]
public class CreateExpenseItemsTables : FluentMigrator.Migration
{
    public override void Up()
    {
        // Expense Items table
        Create.Table("expense_items").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("expense_id").AsGuid().NotNullable()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("amount").AsDecimal(18, 2).NotNullable()
            .WithColumn("paid_by_id").AsGuid().NotNullable();

        Create.ForeignKey("fk_expense_items_expense_id")
            .FromTable("expense_items").InSchema("snapsplit").ForeignColumn("expense_id")
            .ToTable("expenses").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.ForeignKey("fk_expense_items_paid_by_id")
            .FromTable("expense_items").InSchema("snapsplit").ForeignColumn("paid_by_id")
            .ToTable("members").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        // Expense Participants (junction table)
        Create.Table("expense_participants").InSchema("snapsplit")
            .WithColumn("expense_id").AsGuid().NotNullable()
            .WithColumn("member_id").AsGuid().NotNullable();

        Create.PrimaryKey("pk_expense_participants")
            .OnTable("expense_participants").WithSchema("snapsplit")
            .Columns("expense_id", "member_id");

        Create.ForeignKey("fk_expense_participants_expense_id")
            .FromTable("expense_participants").InSchema("snapsplit").ForeignColumn("expense_id")
            .ToTable("expenses").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.ForeignKey("fk_expense_participants_member_id")
            .FromTable("expense_participants").InSchema("snapsplit").ForeignColumn("member_id")
            .ToTable("members").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        // Expense Item Participants (junction table)
        Create.Table("expense_item_participants").InSchema("snapsplit")
            .WithColumn("expense_item_id").AsGuid().NotNullable()
            .WithColumn("member_id").AsGuid().NotNullable();

        Create.PrimaryKey("pk_expense_item_participants")
            .OnTable("expense_item_participants").WithSchema("snapsplit")
            .Columns("expense_item_id", "member_id");

        Create.ForeignKey("fk_expense_item_participants_item_id")
            .FromTable("expense_item_participants").InSchema("snapsplit").ForeignColumn("expense_item_id")
            .ToTable("expense_items").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.ForeignKey("fk_expense_item_participants_member_id")
            .FromTable("expense_item_participants").InSchema("snapsplit").ForeignColumn("member_id")
            .ToTable("members").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Table("expense_item_participants").InSchema("snapsplit");
        Delete.Table("expense_participants").InSchema("snapsplit");
        Delete.Table("expense_items").InSchema("snapsplit");
    }
}
