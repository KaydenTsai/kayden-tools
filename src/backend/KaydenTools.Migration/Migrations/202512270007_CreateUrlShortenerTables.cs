using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

/// <summary>
/// 建立 URL Shortener 相關資料表
/// - short_urls: 儲存短網址對應
/// - url_clicks: 記錄點擊統計
/// </summary>
[Migration(202512270007)]
public class CreateUrlShortenerTables : FluentMigrator.Migration
{
    private new const string Schema = "urlshortener";

    public override void Up()
    {
        Create.Schema(Schema);

        CreateShortUrlsTable();
        CreateUrlClicksTable();
        CreatePartialIndexes();
    }

    public override void Down()
    {
        Delete.Table("url_clicks").InSchema(Schema);
        Delete.Table("short_urls").InSchema(Schema);
        Delete.Schema(Schema);
    }

    private void CreateShortUrlsTable()
    {
        Create.Table("short_urls").InSchema(Schema)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("original_url").AsCustom("text").NotNullable()
            .WithColumn("short_code").AsString(20).NotNullable()
            .WithColumn("owner_id").AsGuid().Nullable()
            .WithColumn("expires_at").AsDateTime().Nullable()
            .WithColumn("click_count").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().Nullable()
            .WithColumn("created_by").AsGuid().Nullable()
            .WithColumn("updated_by").AsGuid().Nullable()
            .WithColumn("is_deleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("deleted_at").AsDateTime().Nullable()
            .WithColumn("deleted_by").AsGuid().Nullable();

        Create.Index("ix_short_urls_owner_id")
            .OnTable("short_urls").InSchema(Schema)
            .OnColumn("owner_id").Ascending();

        Create.Index("ix_short_urls_expires_at")
            .OnTable("short_urls").InSchema(Schema)
            .OnColumn("expires_at").Ascending();

        Create.ForeignKey("fk_short_urls_owner_id")
            .FromTable("short_urls").InSchema(Schema).ForeignColumn("owner_id")
            .ToTable("users").InSchema("public").PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.SetNull);
    }

    private void CreateUrlClicksTable()
    {
        Create.Table("url_clicks").InSchema(Schema)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("short_url_id").AsGuid().NotNullable()
            .WithColumn("clicked_at").AsDateTime().NotNullable()
            .WithColumn("ip_address").AsString(45).Nullable()
            .WithColumn("user_agent").AsString(512).Nullable()
            .WithColumn("referrer").AsString(2048).Nullable()
            .WithColumn("device_type").AsString(20).Nullable();

        Create.Index("ix_url_clicks_short_url_id")
            .OnTable("url_clicks").InSchema(Schema)
            .OnColumn("short_url_id").Ascending();

        Create.Index("ix_url_clicks_clicked_at")
            .OnTable("url_clicks").InSchema(Schema)
            .OnColumn("clicked_at").Descending();

        Create.Index("ix_url_clicks_short_url_clicked")
            .OnTable("url_clicks").InSchema(Schema)
            .OnColumn("short_url_id").Ascending()
            .OnColumn("clicked_at").Descending();

        Create.ForeignKey("fk_url_clicks_short_url_id")
            .FromTable("url_clicks").InSchema(Schema).ForeignColumn("short_url_id")
            .ToTable("short_urls").InSchema(Schema).PrimaryColumn("id")
            .OnDeleteOrUpdate(System.Data.Rule.Cascade);
    }

    private void CreatePartialIndexes()
    {
        Execute.Sql(@"
            CREATE UNIQUE INDEX ix_short_urls_short_code
            ON urlshortener.short_urls (short_code)
            WHERE is_deleted = false;
        ");
    }
}