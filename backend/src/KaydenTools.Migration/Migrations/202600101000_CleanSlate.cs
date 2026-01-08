using FluentMigrator;

namespace KaydenTools.Migration.Migrations;

[Migration(202600101000)]
public class CleanSlate : FluentMigrator.Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            DROP TABLE IF EXISTS public.url_clicks CASCADE;
            DROP TABLE IF EXISTS public.short_urls CASCADE;
            DROP TABLE IF EXISTS public.refresh_tokens CASCADE;
            DROP TABLE IF EXISTS public.users CASCADE;

            DROP TABLE IF EXISTS snapsplit.settled_transfers CASCADE;
            DROP TABLE IF EXISTS snapsplit.expense_item_participants CASCADE;
            DROP TABLE IF EXISTS snapsplit.expense_participants CASCADE;
            DROP TABLE IF EXISTS snapsplit.expense_items CASCADE;
            DROP TABLE IF EXISTS snapsplit.expenses CASCADE;
            DROP TABLE IF EXISTS snapsplit.members CASCADE;
            DROP TABLE IF EXISTS snapsplit.operations CASCADE;
            DROP TABLE IF EXISTS snapsplit.bills CASCADE;

            DROP SCHEMA IF EXISTS snapsplit CASCADE;

            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'VersionInfo') THEN
                    DELETE FROM public.""VersionInfo"" WHERE ""Version"" != 202501210000;
                END IF;
            END $$;
        ");
    }

    public override void Down()
    {
    }
}