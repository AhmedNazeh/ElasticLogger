# Nazeh.ElasticLogger

[![NuGet](https://img.shields.io/nuget/v/Nazeh.ElasticLogger.svg)](https://www.nuget.org/packages/Nazeh.ElasticLogger)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Nazeh.ElasticLogger.svg)](https://www.nuget.org/packages/Nazeh.ElasticLogger)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0%20%7C%209.0-512BD4)](https://dotnet.microsoft.com/)

A powerful, easy-to-use NuGet package for integrating **Elasticsearch** and **Kibana** logging with **Serilog** in .NET applications.

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🎯 **Multi-Target** | Supports .NET 6, 7, 8, and 9 |
| 🔐 **Multiple Auth Methods** | Basic auth, API Key (ES8+), or no auth |
| 🏢 **Cluster Support** | Connect to multiple Elasticsearch nodes |
| 🩺 **Health Checks** | Built-in Elasticsearch health check integration |
| 🔗 **Correlation ID** | Automatic request tracking across services |
| 📊 **Structured Logging** | Rich logging helpers for metrics, events, and API calls |
| ⚡ **ES7 & ES8** | Support for both Elasticsearch 7.x and 8.x |
| 💾 **Dead Letter Queue** | Buffer failed logs for retry |
| 🎨 **Flexible Config** | Configure via appsettings.json or code |
| 📁 **File Logging** | Async file logging with rolling and size limits |
| 🔄 **Backwards Compatible** | Smooth upgrade from v1.x |

## 📦 Installation

```bash
dotnet add package Nazeh.ElasticLogger
```

## 🚀 Quick Start

### Option 1: Minimal API (.NET 6+)

```csharp
using Nazeh.ElasticLogger;

var builder = WebApplication.CreateBuilder(args);

// Add Elastic Logger
builder.Services.AddElasticLogger(builder.Configuration);

var app = builder.Build();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

### Option 2: Host Builder

```csharp
using Nazeh.ElasticLogger;
using Serilog;

var host = Host.CreateDefaultBuilder(args)
    .UseElasticLogger()  // Reads from "ElasticLogger" section
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

try
{
    Log.Information("Application starting...");
    await host.RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

### Option 3: Code-Based Configuration

```csharp
builder.Services.AddElasticLogger(settings =>
{
    settings.ApplicationName = "MyApp";
    settings.Environment = "Production";
    
    // Elasticsearch 8 with API Key
    settings.Elasticsearch.Uri = "https://my-cluster.elastic.co:9243";
    settings.Elasticsearch.Version = ElasticsearchVersion.V8;
    settings.Elasticsearch.AuthMethod = ElasticsearchAuthMethod.ApiKey;
    settings.Elasticsearch.EncodedApiKey = "YOUR_ENCODED_API_KEY";
    
    // Cluster support
    settings.Elasticsearch.AdditionalNodes = new List<string>
    {
        "https://node2.elastic.co:9243",
        "https://node3.elastic.co:9243"
    };
    settings.Elasticsearch.EnableSniffing = true;
});
```

## ⚙️ Configuration

### appsettings.json

```json
{
  "ElasticLogger": {
    "ApplicationName": "MyApplication",
    "Environment": "Production",
    "MinimumLevel": "Information",
    
    "Console": {
      "Enabled": true,
      "UseColors": true
    },
    
    "File": {
      "Enabled": true,
      "Path": "logs/app-.log",
      "RollingInterval": "Day",
      "RetainedFileCount": 31,
      "FileSizeLimitMb": 100
    },
    
    "Elasticsearch": {
      "Uri": "http://localhost:9200",
      "Version": "V8",
      "AuthMethod": "Basic",
      "Username": "elastic",
      "Password": "your-password",
      "IndexPrefix": "app-logs",
      "MinimumLevel": "Information",
      "ValidateServerCertificate": false,
      "BatchPostingLimit": 50,
      "PeriodSeconds": 2
    },
    
    "Kibana": {
      "Uri": "http://localhost:5601"
    },
    
    "Enrichment": {
      "MachineName": true,
      "ThreadId": true,
      "CorrelationId": true,
      "ExceptionDetails": true,
      "CustomProperties": {
        "Team": "Backend",
        "Version": "2.0.0"
      }
    },
    
    "HealthCheck": {
      "Enabled": true,
      "Name": "elasticsearch",
      "TimeoutSeconds": 5
    },
    
    "LogLevelOverrides": {
      "Microsoft": "Warning",
      "System": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### API Key Authentication (Elasticsearch 8+)

```json
{
  "ElasticLogger": {
    "Elasticsearch": {
      "Uri": "https://my-cluster.elastic.co:9243",
      "Version": "V8",
      "AuthMethod": "ApiKey",
      "EncodedApiKey": "YOUR_BASE64_ENCODED_API_KEY"
    }
  }
}
```

Or using ID + Secret:

```json
{
  "ElasticLogger": {
    "Elasticsearch": {
      "AuthMethod": "ApiKey",
      "ApiKeyId": "your-api-key-id",
      "ApiKeySecret": "your-api-key-secret"
    }
  }
}
```

## 🛠️ Advanced Usage

### Structured Logging Helpers

```csharp
using Nazeh.ElasticLogger;
using Serilog;

// Correlation ID scope
using (LoggingHelpers.BeginCorrelationScope("req-123"))
{
    Log.Information("Processing request");
    // All logs in this scope include CorrelationId: req-123
}

// Time an operation
using (Log.Logger.TimeOperation("DatabaseQuery"))
{
    await db.ExecuteAsync(query);
}
// Logs: "Completed operation: DatabaseQuery in 45ms"

// Log API calls
Log.Logger.LogApiCall(
    method: "POST",
    path: "/api/orders",
    statusCode: 201,
    durationMs: 125,
    userId: "user-456",
    correlationId: "req-123"
);

// Log metrics
Log.Logger.LogMetric("OrdersProcessed", 150, "count", 
    tags: new() { ["Region"] = "EU" });

// Log security events
Log.Logger.LogSecurityEvent(
    eventType: "Login",
    userId: "user@example.com",
    ipAddress: "192.168.1.1",
    success: true
);

// Log custom events
Log.Logger.LogEvent("OrderCreated", new()
{
    ["OrderId"] = "ORD-123",
    ["Amount"] = 99.99m,
    ["Currency"] = "USD"
});
```

### Health Checks

```csharp
// In Program.cs
builder.Services.AddElasticLogger(builder.Configuration);

var app = builder.Build();

// Standard health endpoint
app.MapHealthChecks("/health");

// Detailed health endpoint
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Custom Scopes

```csharp
// Add custom properties to a scope
using (LoggingHelpers.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = "123",
    ["OrderId"] = "ORD-456",
    ["Region"] = "EU"
}))
{
    Log.Information("Processing order");
    // All properties included in log context
}
```

## 📋 Configuration Reference

### ElasticLoggerSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApplicationName` | string | "MyApplication" | Application name in logs |
| `Environment` | string | "Production" | Environment name |
| `MinimumLevel` | string | "Information" | Global minimum log level |
| `SuppressInitializationLogs` | bool | false | Hide startup banner |
| `EnableSelfLog` | bool | false | Enable Serilog diagnostics |

### ElasticsearchSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Uri` | string | "http://localhost:9200" | Primary ES URL |
| `AdditionalNodes` | List\<string\> | [] | Additional cluster nodes |
| `Version` | enum | V7 | ES version (V7 or V8) |
| `AuthMethod` | enum | Basic | Authentication method |
| `Username` | string | "elastic" | Basic auth username |
| `Password` | string | "" | Basic auth password |
| `ApiKeyId` | string | null | API key ID |
| `ApiKeySecret` | string | null | API key secret |
| `EncodedApiKey` | string | null | Base64 encoded API key |
| `IndexPrefix` | string | "app-logs" | Index name prefix |
| `BatchPostingLimit` | int | 50 | Events per batch |
| `PeriodSeconds` | int | 2 | Seconds between batches |
| `EnableSniffing` | bool | false | Auto-discover nodes |
| `ValidateServerCertificate` | bool | false | SSL validation |

### EnrichmentSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MachineName` | bool | true | Add machine name |
| `ThreadId` | bool | true | Add thread ID |
| `ProcessId` | bool | true | Add process ID |
| `CorrelationId` | bool | true | Enable correlation tracking |
| `SpanId` | bool | true | Add trace span ID |
| `ExceptionDetails` | bool | true | Rich exception info |
| `CustomProperties` | Dictionary | {} | Static properties |

## 🔄 Migration from v1.x

The v2.0 release maintains backwards compatibility:

```csharp
// Old way (still works, but deprecated)
.UseElasticKibanaLogger()

// New way
.UseElasticLogger()
```

Configuration section names:
- ✅ `"ElasticLogger"` (new, recommended)
- ✅ `"ElasticKibanaLogger"` (legacy, still supported)

## 📊 Viewing Logs in Kibana

1. Open Kibana at `http://localhost:5601`
2. Go to **Stack Management** → **Data Views**
3. Create data view: `app-logs-*`
4. Set time field to `@timestamp`
5. Go to **Discover** to explore logs

### Useful Kibana Queries

```
# Find errors
level: "Error" OR level: "Fatal"

# Filter by application
fields.Application: "MyApp"

# Search by correlation ID
fields.CorrelationId: "req-123"

# Find slow API calls
fields.ApiDurationMs > 1000
```

## 🐛 Troubleshooting

### Logs Not Appearing

1. Check ES connection: `curl http://localhost:9200`
2. Enable self-log: `"EnableSelfLog": true`
3. Check buffer directory for failed events
4. Verify credentials and permissions

### SSL Certificate Errors

```json
{
  "Elasticsearch": {
    "ValidateServerCertificate": false
  }
}
```
⚠️ Only for development! Use proper certificates in production.

### Performance Tips

- Increase `BatchPostingLimit` for high-volume apps
- Use async file logging: `"Async": true`
- Set appropriate `MinimumLevel` to reduce volume
- Consider separate levels for console vs ES

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🤝 Contributing

Contributions welcome! Please open an issue or PR on [GitHub](https://github.com/AhmedNazeh/ElasticLogger).

## 📬 Support

- 🐛 [Report Issues](https://github.com/AhmedNazeh/ElasticLogger/issues)
- 💬 [Discussions](https://github.com/AhmedNazeh/ElasticLogger/discussions)
- 📧 Contact: ahmed.nazeh@outlook.com

---

Made with ❤️ by [Ahmed Nazeh](https://github.com/AhmedNazeh)
