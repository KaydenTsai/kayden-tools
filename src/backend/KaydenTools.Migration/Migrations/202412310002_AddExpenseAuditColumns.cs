using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 修正：新增 expenses 表缺失的審計欄位 (created_by, updated_by)
/// </summary>
[Migration(202412310002)]
public class AddExpenseAuditColumns : FluentMigrator.Migration
{
    public override void Up()
    {
        Alter.Table("expenses").InSchema("snapsplit")
            .AddColumn("created_by").AsGuid().Nullable()
            .AddColumn("updated_by").AsGuid().Nullable();
    }

    public override void Down()
    {
        Delete.Column("created_by").FromTable("expenses").InSchema("snapsplit");
        Delete.Column("updated_by").FromTable("expenses").InSchema("snapsplit");
    }
}
