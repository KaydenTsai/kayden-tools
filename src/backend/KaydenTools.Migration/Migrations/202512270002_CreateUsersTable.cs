using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立使用者資料表
/// - users: 使用者基本資料、OAuth 登入資訊
/// </summary>
[Migration(202512270002)]
public class CreateUsersTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("users").InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("email").AsString(255).Nullable()
            .WithColumn("display_name").AsString(100).Nullable()
            .WithColumn("avatar_url").AsString(500).Nullable()
            .WithColumn("primary_provider").AsInt32().NotNullable()
            .WithColumn("line_user_id").AsString(100).Nullable()
            .WithColumn("line_picture_url").AsString(500).Nullable()
            .WithColumn("google_user_id").AsString(100).Nullable()
            .WithColumn("google_picture_url").AsString(500).Nullable()
            .WithColumn("created_at").AsDateTime2().NotNullable()
            .WithColumn("updated_at").AsDateTime2().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable()
            .WithColumn("is_deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("deleted_at").AsDateTime2().Nullable()
            .WithColumn("deleted_by").AsGuid().Nullable();

        // Partial unique indexes for PostgreSQL (using raw SQL since FluentMigrator doesn't support Filter)
        Execute.Sql(@"
            CREATE UNIQUE INDEX ix_users_email
            ON public.users (email)
            WHERE email IS NOT NULL AND is_deleted = false;
        ");

        Execute.Sql(@"
            CREATE UNIQUE INDEX ix_users_line_user_id
            ON public.users (line_user_id)
            WHERE line_user_id IS NOT NULL AND is_deleted = false;
        ");

        Execute.Sql(@"
            CREATE UNIQUE INDEX ix_users_google_user_id
            ON public.users (google_user_id)
            WHERE google_user_id IS NOT NULL AND is_deleted = false;
        ");
    }

    public override void Down()
    {
        Delete.Table("users").InSchema("public");
    }
}
