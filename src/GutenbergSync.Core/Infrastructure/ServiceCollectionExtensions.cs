using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Infrastructure;
using GutenbergSync.Core.Metadata;
using GutenbergSync.Core.Sync;
using GutenbergSync.Core.Catalog;
using GutenbergSync.Core.Extraction;

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

        // Metadata services
        services.AddSingleton<ILanguageMapper, LanguageMapper>();

        // Sync services
        services.AddScoped<IRsyncService, RsyncService>();

        // Metadata services
        services.AddScoped<IRdfParser, RdfParser>();

        // Catalog services
        services.AddScoped<ICatalogRepository, CatalogRepository>();

        // Extraction services
        services.AddScoped<ITextExtractor, TextExtractor>();

        // Sync orchestration
        services.AddScoped<ISyncOrchestrator, SyncOrchestrator>();

        // TODO: Add core services as they are implemented
        // services.AddScoped<IAuditService, AuditService>();
        // services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();

        return services;
    }
}

