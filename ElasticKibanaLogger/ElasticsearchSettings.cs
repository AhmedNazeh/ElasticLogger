namespace Nazeh.ElasticLogger;

/// <summary>
/// Authentication method for Elasticsearch
/// </summary>
public enum ElasticsearchAuthMethod
{
    /// <summary>
    /// Basic authentication with username and password
    /// </summary>
    Basic,
    
    /// <summary>
    /// API Key authentication (recommended for ES 8+)
    /// </summary>
    ApiKey,
    
    /// <summary>
    /// No authentication (not recommended for production)
    /// </summary>
    None
}

/// <summary>
/// Elasticsearch version to target
/// </summary>
public enum ElasticsearchVersion
{
    /// <summary>
    /// Elasticsearch 7.x
    /// </summary>
    V7,
    
    /// <summary>
    /// Elasticsearch 8.x
    /// </summary>
    V8
}

/// <summary>
/// Configuration settings for Elasticsearch connection
/// </summary>
public class ElasticsearchSettings
{
    /// <summary>
    /// Primary Elasticsearch server URI (e.g., http://localhost:9200)
    /// </summary>
    public string Uri { get; set; } = "http://localhost:9200";

    /// <summary>
    /// Additional Elasticsearch node URIs for cluster support
    /// </summary>
    public List<string> AdditionalNodes { get; set; } = new();

    /// <summary>
    /// Enable node sniffing to automatically discover cluster nodes
    /// </summary>
    public bool EnableSniffing { get; set; } = false;

    /// <summary>
    /// Authentication method to use
    /// </summary>
    public ElasticsearchAuthMethod AuthMethod { get; set; } = ElasticsearchAuthMethod.Basic;

    /// <summary>
    /// Username for Basic authentication
    /// </summary>
    public string Username { get; set; } = "elastic";

    /// <summary>
    /// Password for Basic authentication
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// API Key ID for API Key authentication (ES 8+)
    /// </summary>
    public string? ApiKeyId { get; set; }

    /// <summary>
    /// API Key Secret for API Key authentication (ES 8+)
    /// </summary>
    public string? ApiKeySecret { get; set; }

    /// <summary>
    /// Encoded API Key (alternative to ApiKeyId + ApiKeySecret)
    /// </summary>
    public string? EncodedApiKey { get; set; }

    /// <summary>
    /// Target Elasticsearch version (affects template registration)
    /// </summary>
    public ElasticsearchVersion Version { get; set; } = ElasticsearchVersion.V7;

    /// <summary>
    /// Index prefix for log entries (default: "app-logs")
    /// </summary>
    public string IndexPrefix { get; set; } = "app-logs";

    /// <summary>
    /// Custom index format template. Use {0:yyyy.MM.dd} for date formatting.
    /// If null, uses IndexPrefix-{0:yyyy.MM.dd}
    /// </summary>
    public string? CustomIndexFormat { get; set; }

    /// <summary>
    /// Minimum log level to send to Elasticsearch (default: Information)
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Batch posting limit (default: 50)
    /// </summary>
    public int BatchPostingLimit { get; set; } = 50;

    /// <summary>
    /// Period in seconds between batch posts (default: 2)
    /// </summary>
    public int PeriodSeconds { get; set; } = 2;

    /// <summary>
    /// Connection timeout in seconds (default: 5)
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Number of days to retain log files (default: 7)
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 7;

    /// <summary>
    /// Buffer base filename for failed log shipments (default: "./logs/buffer")
    /// </summary>
    public string BufferBaseFilename { get; set; } = "./logs/buffer";

    /// <summary>
    /// Enable SSL certificate validation (default: false for development)
    /// </summary>
    public bool ValidateServerCertificate { get; set; } = false;

    /// <summary>
    /// Enable inline fields for better Kibana compatibility
    /// </summary>
    public bool InlineFields { get; set; } = true;

    /// <summary>
    /// Number of shards for the index template (default: 1)
    /// </summary>
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Number of replicas for the index template (default: 1)
    /// </summary>
    public int NumberOfReplicas { get; set; } = 1;

    /// <summary>
    /// Enable dead letter queue for failed events
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Maximum retries for failed log shipments
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets all Elasticsearch URIs (primary + additional nodes)
    /// </summary>
    public IEnumerable<Uri> GetAllUris()
    {
        yield return new Uri(Uri);
        foreach (var node in AdditionalNodes)
        {
            yield return new Uri(node);
        }
    }
}

/// <summary>
/// Configuration settings for Kibana connection
/// </summary>
public class KibanaSettings
{
    /// <summary>
    /// Kibana server URI (e.g., http://localhost:5601)
    /// </summary>
    public string Uri { get; set; } = "http://localhost:5601";

    /// <summary>
    /// Username for Kibana authentication (optional, uses Elasticsearch credentials by default)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for Kibana authentication (optional, uses Elasticsearch credentials by default)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Default space ID in Kibana (optional)
    /// </summary>
    public string? SpaceId { get; set; }
}

/// <summary>
/// Console output formatting options
/// </summary>
public class ConsoleSettings
{
    /// <summary>
    /// Enable console logging
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Output template for console logging
    /// </summary>
    public string OutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Enable ANSI color codes in console output
    /// </summary>
    public bool UseColors { get; set; } = true;

    /// <summary>
    /// Minimum log level for console output (overrides global level if set)
    /// </summary>
    public string? MinimumLevel { get; set; }
}

/// <summary>
/// File logging configuration options
/// </summary>
public class FileSettings
{
    /// <summary>
    /// Enable file logging
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// File log path (default: "logs/app-.log")
    /// </summary>
    public string Path { get; set; } = "logs/app-.log";

    /// <summary>
    /// Output template for file logging
    /// </summary>
    public string OutputTemplate { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Rolling interval for log files
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Maximum file size in MB before rolling (default: 100MB)
    /// </summary>
    public int FileSizeLimitMb { get; set; } = 100;

    /// <summary>
    /// Number of log files to retain (default: 31)
    /// </summary>
    public int RetainedFileCount { get; set; } = 31;

    /// <summary>
    /// Enable shared file access (allows multiple processes to write)
    /// </summary>
    public bool Shared { get; set; } = false;

    /// <summary>
    /// Enable async file writing for better performance
    /// </summary>
    public bool Async { get; set; } = true;

    /// <summary>
    /// Minimum log level for file output (overrides global level if set)
    /// </summary>
    public string? MinimumLevel { get; set; }
}

/// <summary>
/// Enrichment configuration options
/// </summary>
public class EnrichmentSettings
{
    /// <summary>
    /// Enable machine name enrichment
    /// </summary>
    public bool MachineName { get; set; } = true;

    /// <summary>
    /// Enable thread ID enrichment
    /// </summary>
    public bool ThreadId { get; set; } = true;

    /// <summary>
    /// Enable environment name enrichment
    /// </summary>
    public bool EnvironmentName { get; set; } = true;

    /// <summary>
    /// Enable process ID enrichment
    /// </summary>
    public bool ProcessId { get; set; } = true;

    /// <summary>
    /// Enable correlation ID enrichment (for request tracking)
    /// </summary>
    public bool CorrelationId { get; set; } = true;

    /// <summary>
    /// Enable span ID enrichment (for distributed tracing)
    /// </summary>
    public bool SpanId { get; set; } = true;

    /// <summary>
    /// Enable exception details enrichment
    /// </summary>
    public bool ExceptionDetails { get; set; } = true;

    /// <summary>
    /// Custom properties to add to all log entries
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new();
}

/// <summary>
/// Health check configuration options
/// </summary>
public class HealthCheckSettings
{
    /// <summary>
    /// Enable Elasticsearch health checks
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check name
    /// </summary>
    public string Name { get; set; } = "elasticsearch";

    /// <summary>
    /// Health check timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Tags for health check filtering
    /// </summary>
    public List<string> Tags { get; set; } = new() { "ready", "elasticsearch" };
}

/// <summary>
/// Combined logging configuration settings
/// </summary>
public class ElasticLoggerSettings
{
    /// <summary>
    /// Application name to be included in logs
    /// </summary>
    public string ApplicationName { get; set; } = "MyApplication";

    /// <summary>
    /// Environment name (e.g., Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Global minimum log level
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Enable console logging (shorthand for Console.Enabled)
    /// </summary>
    public bool EnableConsoleLogging
    {
        get => Console.Enabled;
        set => Console.Enabled = value;
    }

    /// <summary>
    /// Enable file logging (shorthand for File.Enabled)
    /// </summary>
    public bool EnableFileLogging
    {
        get => File.Enabled;
        set => File.Enabled = value;
    }

    /// <summary>
    /// File log path (shorthand for File.Path)
    /// </summary>
    public string FileLogPath
    {
        get => File.Path;
        set => File.Path = value;
    }

    /// <summary>
    /// Console logging configuration
    /// </summary>
    public ConsoleSettings Console { get; set; } = new();

    /// <summary>
    /// File logging configuration
    /// </summary>
    public FileSettings File { get; set; } = new();

    /// <summary>
    /// Elasticsearch configuration
    /// </summary>
    public ElasticsearchSettings Elasticsearch { get; set; } = new();

    /// <summary>
    /// Kibana configuration
    /// </summary>
    public KibanaSettings Kibana { get; set; } = new();

    /// <summary>
    /// Enrichment configuration
    /// </summary>
    public EnrichmentSettings Enrichment { get; set; } = new();

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckSettings HealthCheck { get; set; } = new();

    /// <summary>
    /// Log level overrides for specific namespaces
    /// </summary>
    public Dictionary<string, string> LogLevelOverrides { get; set; } = new()
    {
        ["Microsoft"] = "Warning",
        ["System"] = "Warning",
        ["Microsoft.Hosting.Lifetime"] = "Information"
    };

    /// <summary>
    /// Enable self-logging for Serilog diagnostics
    /// </summary>
    public bool EnableSelfLog { get; set; } = false;

    /// <summary>
    /// Suppress initialization log messages
    /// </summary>
    public bool SuppressInitializationLogs { get; set; } = false;
}

// Backwards compatibility alias
/// <summary>
/// Alias for ElasticLoggerSettings for backwards compatibility
/// </summary>
public class ElasticKibanaLoggerSettings : ElasticLoggerSettings { }
