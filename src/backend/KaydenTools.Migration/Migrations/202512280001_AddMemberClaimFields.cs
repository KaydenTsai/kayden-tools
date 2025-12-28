using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 新增成員認領相關欄位
/// - original_name: 原始名稱（認領前的名稱，用於取消認領時還原）
/// - claimed_at: 認領時間
/// </summary>
[Migration(202512280001)]
public class AddMemberClaimFields : FluentMigrator.Migration
{
    public override void Up()
    {
        Alter.Table("members").InSchema("snapsplit")
            .AddColumn("original_name").AsString(50).Nullable()
            .AddColumn("claimed_at").AsDateTime2().Nullable();

        // 新增 linked_user_id 的外鍵（如果之前沒有）
        // 以及相關索引
        Create.Index("ix_members_linked_user_id")
            .OnTable("members").InSchema("snapsplit")
            .OnColumn("linked_user_id").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_members_linked_user_id")
            .OnTable("members").InSchema("snapsplit");

        Delete.Column("original_name").FromTable("members").InSchema("snapsplit");
        Delete.Column("claimed_at").FromTable("members").InSchema("snapsplit");
    }
}
