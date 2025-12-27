using KaydenTools.Services.Interfaces;
using KaydenTools.Services.SnapSplit;
using KaydenTools.Services.UrlShortener;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace KaydenTools.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        // SnapSplit Services
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<ISettlementService, SettlementService>();

        // UrlShortener Services
        services.AddScoped<IShortUrlService, ShortUrlService>();
        services.AddSingleton<ClickTrackingChannel>();
        services.AddHostedService<ClickTrackingService>();

        return services;
    }
}
