using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立結算資料表
/// - settlements: 成員間轉帳結算記錄
/// </summary>
[Migration(202512270006)]
public class CreateSettlementsTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("settlements").InSchema("snapsplit")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("bill_id").AsGuid().NotNullable()
            .WithColumn("from_member_id").AsGuid().NotNullable()
            .WithColumn("to_member_id").AsGuid().NotNullable()
            .WithColumn("amount").AsDecimal(18, 2).NotNullable()
            .WithColumn("is_settled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("settled_at").AsDateTime2().Nullable();

        Create.ForeignKey("fk_settlements_bill_id")
            .FromTable("settlements").InSchema("snapsplit").ForeignColumn("bill_id")
            .ToTable("bills").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.ForeignKey("fk_settlements_from_member_id")
            .FromTable("settlements").InSchema("snapsplit").ForeignColumn("from_member_id")
            .ToTable("members").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.ForeignKey("fk_settlements_to_member_id")
            .FromTable("settlements").InSchema("snapsplit").ForeignColumn("to_member_id")
            .ToTable("members").InSchema("snapsplit").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);

        Create.Index("ix_settlements_bill_members")
            .OnTable("settlements").InSchema("snapsplit")
            .OnColumn("bill_id").Ascending()
            .OnColumn("from_member_id").Ascending()
            .OnColumn("to_member_id").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("settlements").InSchema("snapsplit");
    }
}
