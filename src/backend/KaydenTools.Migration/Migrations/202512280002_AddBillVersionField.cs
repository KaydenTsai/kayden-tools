using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 替 Bills 資料表新增版本號欄位，用於樂觀鎖協作同步
/// </summary>
[Migration(202512280002)]
public class AddBillVersionField : FluentMigrator.Migration
{
    public override void Up()
    {
        Alter.Table("bills").InSchema("snapsplit")
            .AddColumn("version").AsInt64().NotNullable().WithDefaultValue(1);
    }

    public override void Down()
    {
        Delete.Column("version").FromTable("bills").InSchema("snapsplit");
    }
}
