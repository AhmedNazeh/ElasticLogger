using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;

namespace Nazeh.ElasticLogger;

/// <summary>
/// Extension methods for configuring Elastic Logger
/// </summary>
public static class ElasticLoggerExtensions
{
    private const string DefaultConfigSection = "ElasticLogger";
    private const string LegacyConfigSection = "ElasticKibanaLogger";

    #region IHostBuilder Extensions

    /// <summary>
    /// Adds Elastic logging to the host builder using configuration from appsettings.json
    /// </summary>
    /// <param name="builder">The host builder</param>
    /// <param name="configurationSectionName">Configuration section name (default: "ElasticLogger")</param>
    /// <returns>The host builder for chaining</returns>
    public static IHostBuilder UseElasticLogger(
        this IHostBuilder builder,
        string? configurationSectionName = null)
    {
        return builder.UseSerilog((context, services, configuration) =>
        {
            var settings = ResolveSettings(context.Configuration, configurationSectionName);
            ConfigureLogger(configuration, settings);
        });
    }

    /// <summary>
    /// Adds Elastic logging to the host builder with custom settings
    /// </summary>
    /// <param name="builder">The host builder</param>
    /// <param name="configureSettings">Action to configure settings</param>
    /// <returns>The host builder for chaining</returns>
    public static IHostBuilder UseElasticLogger(
        this IHostBuilder builder,
        Action<ElasticLoggerSettings> configureSettings)
    {
        var settings = new ElasticLoggerSettings();
        configureSettings(settings);

        return builder.UseSerilog((context, services, configuration) =>
        {
            ConfigureLogger(configuration, settings);
        });
    }

    /// <summary>
    /// Backwards compatible method - Adds ElasticKibana logging to the host builder
    /// </summary>
    [Obsolete("Use UseElasticLogger instead. This method will be removed in a future version.")]
    public static IHostBuilder UseElasticKibanaLogger(
        this IHostBuilder builder,
        string configurationSectionName = LegacyConfigSection)
    {
        return builder.UseElasticLogger(configurationSectionName);
    }

    /// <summary>
    /// Backwards compatible method - Adds ElasticKibana logging with custom settings
    /// </summary>
    [Obsolete("Use UseElasticLogger instead. This method will be removed in a future version.")]
    public static IHostBuilder UseElasticKibanaLogger(
        this IHostBuilder builder,
        Action<ElasticKibanaLoggerSettings> configureSettings)
    {
        return builder.UseElasticLogger(settings =>
        {
            var legacySettings = new ElasticKibanaLoggerSettings();
            configureSettings(legacySettings);
            // Copy properties from legacy to current settings
            settings.ApplicationName = legacySettings.ApplicationName;
            settings.Environment = legacySettings.Environment;
            settings.EnableConsoleLogging = legacySettings.EnableConsoleLogging;
            settings.EnableFileLogging = legacySettings.EnableFileLogging;
            settings.FileLogPath = legacySettings.FileLogPath;
            settings.Elasticsearch = legacySettings.Elasticsearch;
            settings.Kibana = legacySettings.Kibana;
        });
    }

    #endregion

    #region IServiceCollection Extensions

    /// <summary>
    /// Adds Elastic Logger services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="configurationSectionName">Configuration section name</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddElasticLogger(
        this IServiceCollection services,
        IConfiguration configuration,
        string? configurationSectionName = null)
    {
        var settings = ResolveSettings(configuration, configurationSectionName);
        
        // Register settings
        services.AddSingleton(settings);
        
        // Configure Serilog
        var loggerConfig = new LoggerConfiguration();
        ConfigureLogger(loggerConfig, settings);
        Log.Logger = loggerConfig.CreateLogger();
        
        // Add Serilog services
        services.AddLogging(builder => builder.AddSerilog(dispose: true));
        
        // Add health check if enabled
        if (settings.HealthCheck.Enabled)
        {
            services.AddElasticLoggerHealthCheck(settings);
        }
        
        // Add correlation ID services
        if (settings.Enrichment.CorrelationId)
        {
            services.AddScoped<CorrelationIdContext>();
        }
        
        return services;
    }

    /// <summary>
    /// Adds Elastic Logger services with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureSettings">Action to configure settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddElasticLogger(
        this IServiceCollection services,
        Action<ElasticLoggerSettings> configureSettings)
    {
        var settings = new ElasticLoggerSettings();
        configureSettings(settings);
        
        services.AddSingleton(settings);
        
        var loggerConfig = new LoggerConfiguration();
        ConfigureLogger(loggerConfig, settings);
        Log.Logger = loggerConfig.CreateLogger();
        
        services.AddLogging(builder => builder.AddSerilog(dispose: true));
        
        if (settings.HealthCheck.Enabled)
        {
            services.AddElasticLoggerHealthCheck(settings);
        }
        
        if (settings.Enrichment.CorrelationId)
        {
            services.AddScoped<CorrelationIdContext>();
        }
        
        return services;
    }

    /// <summary>
    /// Adds Elasticsearch health check
    /// </summary>
    private static IServiceCollection AddElasticLoggerHealthCheck(
        this IServiceCollection services,
        ElasticLoggerSettings settings)
    {
        services.AddHealthChecks()
            .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                settings.HealthCheck.Name,
                sp => new ElasticsearchHealthCheck(settings),
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: settings.HealthCheck.Tags,
                timeout: TimeSpan.FromSeconds(settings.HealthCheck.TimeoutSeconds)));
        
        return services;
    }

    #endregion

    #region Logger Configuration

    /// <summary>
    /// Configures Serilog logger with Elasticsearch support
    /// </summary>
    /// <param name="configuration">Logger configuration</param>
    /// <param name="settings">Elastic Logger settings</param>
    public static void ConfigureLogger(
        LoggerConfiguration configuration,
        ElasticLoggerSettings settings)
    {
        // Enable self-log if requested
        if (settings.EnableSelfLog)
        {
            Serilog.Debugging.SelfLog.Enable(Console.Error);
        }

        // Parse minimum log level
        var minLevel = Enum.Parse<LogEventLevel>(settings.MinimumLevel, true);
        var esMinLevel = Enum.Parse<LogEventLevel>(settings.Elasticsearch.MinimumLevel, true);

        // Base configuration
        configuration.MinimumLevel.Is(minLevel);

        // Apply log level overrides
        foreach (var (ns, level) in settings.LogLevelOverrides)
        {
            var overrideLevel = Enum.Parse<LogEventLevel>(level, true);
            configuration.MinimumLevel.Override(ns, overrideLevel);
        }

        // Enrichers
        configuration.Enrich.FromLogContext();
        
        if (settings.Enrichment.MachineName)
            configuration.Enrich.WithMachineName();
        
        if (settings.Enrichment.ThreadId)
            configuration.Enrich.WithThreadId();
        
        if (settings.Enrichment.ProcessId)
            configuration.Enrich.WithProperty("ProcessId", Environment.ProcessId);
        
        if (settings.Enrichment.ExceptionDetails)
            configuration.Enrich.WithExceptionDetails();
        
        if (settings.Enrichment.SpanId)
            configuration.Enrich.WithProperty("SpanId", System.Diagnostics.Activity.Current?.SpanId.ToString() ?? "none");

        // Standard properties
        configuration
            .Enrich.WithProperty("Application", settings.ApplicationName)
            .Enrich.WithProperty("Environment", settings.Environment);

        // Custom properties
        foreach (var (key, value) in settings.Enrichment.CustomProperties)
        {
            configuration.Enrich.WithProperty(key, value);
        }

        // Console logging
        if (settings.Console.Enabled)
        {
            var consoleMinLevel = settings.Console.MinimumLevel != null 
                ? Enum.Parse<LogEventLevel>(settings.Console.MinimumLevel, true) 
                : minLevel;
            
            configuration.WriteTo.Console(
                restrictedToMinimumLevel: consoleMinLevel,
                outputTemplate: settings.Console.OutputTemplate,
                theme: settings.Console.UseColors 
                    ? Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code 
                    : null);
        }

        // File logging
        if (settings.File.Enabled)
        {
            var fileMinLevel = settings.File.MinimumLevel != null
                ? Enum.Parse<LogEventLevel>(settings.File.MinimumLevel, true)
                : minLevel;
            
            var rollingInterval = Enum.Parse<RollingInterval>(settings.File.RollingInterval, true);
            
            configuration.WriteTo.File(
                path: settings.File.Path,
                restrictedToMinimumLevel: fileMinLevel,
                rollingInterval: rollingInterval,
                retainedFileCountLimit: settings.File.RetainedFileCount,
                fileSizeLimitBytes: settings.File.FileSizeLimitMb * 1024 * 1024,
                shared: settings.File.Shared,
                outputTemplate: settings.File.OutputTemplate);
        }

        // Elasticsearch logging
        ConfigureElasticsearch(configuration, settings, esMinLevel);

        // Log initialization info
        if (!settings.SuppressInitializationLogs)
        {
            Log.Information("╔══════════════════════════════════════════════════════════╗");
            Log.Information("║          Nazeh.ElasticLogger Initialized v2.0           ║");
            Log.Information("╠══════════════════════════════════════════════════════════╣");
            Log.Information("║ Application: {ApplicationName,-40} ║", settings.ApplicationName);
            Log.Information("║ Environment: {Environment,-40} ║", settings.Environment);
            Log.Information("║ ES URL:      {ElasticUrl,-40} ║", settings.Elasticsearch.Uri);
            Log.Information("║ ES Version:  {Version,-40} ║", settings.Elasticsearch.Version);
            Log.Information("║ Index:       {IndexPrefix,-40} ║", settings.Elasticsearch.IndexPrefix + "-*");
            Log.Information("╚══════════════════════════════════════════════════════════╝");
        }
    }

    /// <summary>
    /// Configures Elasticsearch sink with all options
    /// </summary>
    private static void ConfigureElasticsearch(
        LoggerConfiguration configuration,
        ElasticLoggerSettings settings,
        LogEventLevel minLevel)
    {
        var esSettings = settings.Elasticsearch;
        var indexFormat = esSettings.CustomIndexFormat ?? $"{esSettings.IndexPrefix}-{{0:yyyy.MM.dd}}";
        
        var templateVersion = esSettings.Version == ElasticsearchVersion.V8 
            ? AutoRegisterTemplateVersion.ESv8 
            : AutoRegisterTemplateVersion.ESv7;

        var sinkOptions = new ElasticsearchSinkOptions(esSettings.GetAllUris().ToArray())
        {
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = templateVersion,
            IndexFormat = indexFormat,
            TemplateName = $"{esSettings.IndexPrefix}-template",
            BatchPostingLimit = esSettings.BatchPostingLimit,
            Period = TimeSpan.FromSeconds(esSettings.PeriodSeconds),
            InlineFields = esSettings.InlineFields,
            MinimumLogEventLevel = minLevel,
            NumberOfShards = esSettings.NumberOfShards,
            NumberOfReplicas = esSettings.NumberOfReplicas,
            EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog | 
                              EmitEventFailureHandling.WriteToFailureSink,
            FailureCallback = (logEvent, exception) =>
            {
                Console.Error.WriteLine($"[Nazeh.ElasticLogger] Failed to ship log: {exception?.Message}");
            },
            BufferBaseFilename = esSettings.EnableDeadLetterQueue ? esSettings.BufferBaseFilename : null,
            BufferLogShippingInterval = TimeSpan.FromSeconds(5),
            ModifyConnectionSettings = conn =>
            {
                var connection = conn.RequestTimeout(TimeSpan.FromSeconds(esSettings.ConnectionTimeoutSeconds));

                // Configure authentication
                switch (esSettings.AuthMethod)
                {
                    case ElasticsearchAuthMethod.Basic:
                        connection.BasicAuthentication(esSettings.Username, esSettings.Password);
                        break;
                    
                    case ElasticsearchAuthMethod.ApiKey:
                        if (!string.IsNullOrEmpty(esSettings.EncodedApiKey))
                        {
                            connection.ApiKeyAuthentication(esSettings.EncodedApiKey, string.Empty);
                        }
                        else if (!string.IsNullOrEmpty(esSettings.ApiKeyId) && !string.IsNullOrEmpty(esSettings.ApiKeySecret))
                        {
                            connection.ApiKeyAuthentication(esSettings.ApiKeyId, esSettings.ApiKeySecret);
                        }
                        break;
                }

                // SSL configuration
                if (!esSettings.ValidateServerCertificate)
                {
                    connection.ServerCertificateValidationCallback((o, cert, chain, errors) => true);
                }

                // Sniffing for cluster
                if (esSettings.EnableSniffing && esSettings.AdditionalNodes.Any())
                {
                    connection.SniffOnStartup(true);
                    connection.SniffOnConnectionFault(true);
                }

                return connection;
            }
        };

        // TypeName is deprecated in ES8
        if (esSettings.Version == ElasticsearchVersion.V7)
        {
            sinkOptions.TypeName = "_doc";
        }

        configuration.WriteTo.Elasticsearch(sinkOptions);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates and configures a standalone Serilog logger
    /// </summary>
    /// <param name="settings">Elastic Logger settings</param>
    /// <returns>Configured ILogger instance</returns>
    public static ILogger CreateLogger(ElasticLoggerSettings settings)
    {
        var configuration = new LoggerConfiguration();
        ConfigureLogger(configuration, settings);
        return configuration.CreateLogger();
    }

    /// <summary>
    /// Creates and configures a standalone Serilog logger from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing ElasticLogger section</param>
    /// <param name="configurationSectionName">Configuration section name</param>
    /// <returns>Configured ILogger instance</returns>
    public static ILogger CreateLogger(
        IConfiguration configuration,
        string? configurationSectionName = null)
    {
        var settings = ResolveSettings(configuration, configurationSectionName);
        return CreateLogger(settings);
    }

    /// <summary>
    /// Backwards compatible method
    /// </summary>
    [Obsolete("Use CreateLogger instead")]
    public static ILogger CreateElasticKibanaLogger(ElasticKibanaLoggerSettings settings) 
        => CreateLogger(settings);

    /// <summary>
    /// Backwards compatible method
    /// </summary>
    [Obsolete("Use CreateLogger instead")]
    public static ILogger CreateElasticKibanaLogger(
        IConfiguration configuration,
        string configurationSectionName = LegacyConfigSection) 
        => CreateLogger(configuration, configurationSectionName);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Resolves settings from configuration, checking multiple section names
    /// </summary>
    private static ElasticLoggerSettings ResolveSettings(
        IConfiguration configuration,
        string? explicitSectionName)
    {
        var settings = new ElasticLoggerSettings();
        
        // Try explicit section name first
        if (!string.IsNullOrEmpty(explicitSectionName))
        {
            var section = configuration.GetSection(explicitSectionName);
            if (section.Exists())
            {
                section.Bind(settings);
                return settings;
            }
        }
        
        // Try default section
        var defaultSection = configuration.GetSection(DefaultConfigSection);
        if (defaultSection.Exists())
        {
            defaultSection.Bind(settings);
            return settings;
        }
        
        // Try legacy section for backwards compatibility
        var legacySection = configuration.GetSection(LegacyConfigSection);
        if (legacySection.Exists())
        {
            legacySection.Bind(settings);
        }
        
        return settings;
    }

    #endregion
}

/// <summary>
/// Context for correlation ID tracking across requests
/// </summary>
public class CorrelationIdContext
{
    /// <summary>
    /// The current correlation ID
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
}

// Backwards compatibility aliases
/// <summary>
/// Backwards compatible alias
/// </summary>
public static class ElasticKibanaLoggerExtensions
{
    /// <summary>
    /// Backwards compatible method
    /// </summary>
    [Obsolete("Use ElasticLoggerExtensions.UseElasticLogger instead")]
    public static IHostBuilder UseElasticKibanaLogger(
        this IHostBuilder builder,
        string configurationSectionName = "ElasticKibanaLogger")
        => ElasticLoggerExtensions.UseElasticLogger(builder, configurationSectionName);

    /// <summary>
    /// Backwards compatible method
    /// </summary>
    [Obsolete("Use ElasticLoggerExtensions.UseElasticLogger instead")]
    public static IHostBuilder UseElasticKibanaLogger(
        this IHostBuilder builder,
        Action<ElasticKibanaLoggerSettings> configureSettings)
    {
        return builder.UseElasticLogger(settings =>
        {
            var legacySettings = new ElasticKibanaLoggerSettings();
            configureSettings(legacySettings);
            settings.ApplicationName = legacySettings.ApplicationName;
            settings.Environment = legacySettings.Environment;
            settings.EnableConsoleLogging = legacySettings.EnableConsoleLogging;
            settings.EnableFileLogging = legacySettings.EnableFileLogging;
            settings.FileLogPath = legacySettings.FileLogPath;
            settings.Elasticsearch = legacySettings.Elasticsearch;
            settings.Kibana = legacySettings.Kibana;
        });
    }

    /// <summary>
    /// Backwards compatible method
    /// </summary>
    [Obsolete("Use ElasticLoggerExtensions.CreateLogger instead")]
    public static ILogger CreateElasticKibanaLogger(IConfiguration configuration, string configurationSectionName = "ElasticKibanaLogger")
        => ElasticLoggerExtensions.CreateLogger(configuration, configurationSectionName);

    /// <summary>
    /// Backwards compatible method
    /// </summary>
    [Obsolete("Use ElasticLoggerExtensions.CreateLogger instead")]
    public static ILogger CreateElasticKibanaLogger(ElasticKibanaLoggerSettings settings)
        => ElasticLoggerExtensions.CreateLogger(settings);
}
