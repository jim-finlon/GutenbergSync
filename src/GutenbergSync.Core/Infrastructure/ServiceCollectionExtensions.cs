using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Infrastructure;

namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Extension methods for configuring dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all GutenbergSync core services to the service collection
    /// </summary>
    public static IServiceCollection AddGutenbergSyncCore(this IServiceCollection services)
    {
        // Configuration
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();

        // Infrastructure
        services.AddSingleton<IRsyncDiscoveryService, RsyncDiscoveryService>();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();

        // TODO: Add core services as they are implemented
        // services.AddScoped<IRsyncService, RsyncService>();
        // services.AddScoped<IRdfParser, RdfParser>();
        // services.AddScoped<ILanguageMapper, LanguageMapper>();
        // services.AddScoped<ICatalogRepository, CatalogRepository>();
        // services.AddScoped<ITextExtractor, TextExtractor>();
        // services.AddScoped<IAuditService, AuditService>();
        // services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();

        return services;
    }
}

