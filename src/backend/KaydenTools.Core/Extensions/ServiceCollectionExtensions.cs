using Kayden.Commons.Interfaces;
using Kayden.Commons.Services;
using KaydenTools.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KaydenTools.Core.Extensions;

/// <summary>
/// 服務容器擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊核心服務
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddSingleton<AppSettingManager>();
        return services;
    }

    /// <summary>
    /// 從設定檔取得並註冊設定物件
    /// </summary>
    public static T GetSettings<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class, new()
    {
        var manager = new AppSettingManager(configuration);
        var settings = manager.GetSettings<T>();
        services.AddSingleton(settings);
        return settings;
    }
}
