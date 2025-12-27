using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace KaydenTools.Migration.Extensions;

/// <summary>
/// Migration 服務註冊擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 FluentMigrator 服務
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="connectionString">資料庫連線字串</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddMigrations(this IServiceCollection services, string connectionString)
    {
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(ServiceCollectionExtensions).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }
}
