using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立 Refresh Token 資料表
/// - refresh_tokens: JWT 刷新令牌
/// </summary>
[Migration(202512270003)]
public class CreateRefreshTokensTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("refresh_tokens").InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("user_id").AsGuid().NotNullable()
            .WithColumn("token").AsString(500).NotNullable()
            .WithColumn("device_info").AsString(500).Nullable()
            .WithColumn("ip_address").AsString(45).Nullable()
            .WithColumn("expires_at").AsDateTime2().NotNullable()
            .WithColumn("created_at").AsDateTime2().NotNullable()
            .WithColumn("revoked_at").AsDateTime2().Nullable();

        Create.Index("ix_refresh_tokens_token")
            .OnTable("refresh_tokens").InSchema("public")
            .OnColumn("token").Ascending()
            .WithOptions().Unique();

        Create.ForeignKey("fk_refresh_tokens_user_id")
            .FromTable("refresh_tokens").InSchema("public").ForeignColumn("user_id")
            .ToTable("users").InSchema("public").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Table("refresh_tokens").InSchema("public");
    }
}
