using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立資料庫 Schema
/// - snapsplit: SnapSplit 分帳功能
/// </summary>
[Migration(202512270001)]
public class CreateSchemas : FluentMigrator.Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS snapsplit;");
    }

    public override void Down()
    {
        Execute.Sql("DROP SCHEMA IF EXISTS snapsplit CASCADE;");
    }
}
