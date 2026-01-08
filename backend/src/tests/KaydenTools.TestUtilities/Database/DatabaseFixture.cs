using FluentMigrator.Runner;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Interfaces;
using KaydenTools.Migration.Extensions;
using KaydenTools.Repositories;
using KaydenTools.Repositories.Implementations;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Extensions;
using KaydenTools.TestUtilities.Common;
using KaydenTools.TestUtilities.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace KaydenTools.TestUtilities.Database;

/// <summary>
/// 資料庫測試固定裝置
/// 管理連線、Migration、DI 容器
/// </summary>
public class DatabaseFixture : IDisposable
{
    private readonly TestDatabaseOptions _options;
    private readonly Action<IServiceCollection>? _configureServices;
    private bool _isInitialized;
    private IServiceProvider? _rootProvider;

    public string ConnectionString => _options.ConnectionString;

    public DatabaseFixture(TestDatabaseOptions options, Action<IServiceCollection>? configureServices = null)
    {
        _options = options;
        _configureServices = configureServices;
    }

    /// <summary>
    /// 初始化資料庫（執行 Migration）
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        if (_options.RunMigrations)
        {
            RunMigrations();
        }

        _rootProvider = CreateServiceProvider();
        _isInitialized = true;
    }

    /// <summary>
    /// 建立新的 Service Scope
    /// </summary>
    public IServiceScope CreateScope()
    {
        if (_rootProvider == null)
            throw new InvalidOperationException("DatabaseFixture 尚未初始化，請先呼叫 Initialize()");

        return _rootProvider.CreateScope();
    }

    /// <summary>
    /// 建立 ServiceProvider
    /// </summary>
    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // 先執行自訂配置（優先權最高）
        _configureServices?.Invoke(services);

        // 註冊測試用的服務
        services.TryAddSingleton<IDateTimeService, FakeDateTimeService>();
        services.TryAddSingleton<ICurrentUserService, FakeCurrentUserService>();
        services.TryAddSingleton<IBillNotificationService, FakeBillNotificationService>();

        // DbContext - 手動設定，不使用 AddRepositories (因為它會覆蓋配置)
        services.TryAddScoped(sp =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options;

            var dateTimeService = sp.GetRequiredService<IDateTimeService>();
            var currentUserService = sp.GetRequiredService<ICurrentUserService>();

            return new AppDbContext(options, dateTimeService, currentUserService);
        });

        // UnitOfWork
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddServices();

        // Logging（降低層級避免測試輸出太多）
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 建立新的 DbContext (供直接存取)
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new FakeDateTimeService(), new FakeCurrentUserService());
    }

    /// <summary>
    /// 清空所有測試資料
    /// </summary>
    /// <remarks>
    /// 按照外鍵依賴順序清空（子表 → 父表）
    /// </remarks>
    public async Task CleanupAsync()
    {
        await using var context = CreateDbContext();

        foreach (var table in DbTableNames.AllTablesInCleanupOrder)
        {
            try
            {
#pragma warning disable EF1002 // Risk of SQL injection - table names are from constant class
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {table} CASCADE;");
#pragma warning restore EF1002
            }
            catch (Exception)
            {
                // 表可能不存在，忽略錯誤
            }
        }
    }

    /// <summary>
    /// 執行 Migration
    /// </summary>
    private void RunMigrations()
    {
        var services = new ServiceCollection();

        services.AddMigrations(ConnectionString);

        using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<IMigrationRunner>();

        runner.MigrateUp();
    }

    public void Dispose()
    {
        (_rootProvider as IDisposable)?.Dispose();
    }
}
