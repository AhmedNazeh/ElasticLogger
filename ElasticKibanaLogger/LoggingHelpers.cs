using Serilog;
using Serilog.Context;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nazeh.ElasticLogger;

/// <summary>
/// Helper methods for structured logging patterns
/// </summary>
public static class LoggingHelpers
{
    /// <summary>
    /// Creates a logging scope with a correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation ID (auto-generated if null)</param>
    /// <returns>Disposable scope that removes the correlation ID when disposed</returns>
    public static IDisposable BeginCorrelationScope(string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString("N");
        return LogContext.PushProperty("CorrelationId", correlationId);
    }

    /// <summary>
    /// Creates a logging scope with custom properties
    /// </summary>
    /// <param name="properties">Dictionary of properties to add to log context</param>
    /// <returns>Disposable scope that removes properties when disposed</returns>
    public static IDisposable BeginScope(Dictionary<string, object> properties)
    {
        var disposables = new List<IDisposable>();
        foreach (var (key, value) in properties)
        {
            disposables.Add(LogContext.PushProperty(key, value));
        }
        return new CompositeDisposable(disposables);
    }

    /// <summary>
    /// Creates a logging scope with a single property
    /// </summary>
    public static IDisposable BeginScope(string propertyName, object propertyValue)
    {
        return LogContext.PushProperty(propertyName, propertyValue);
    }

    /// <summary>
    /// Logs operation timing with automatic duration calculation
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="operationName">Name of the operation being timed</param>
    /// <returns>Disposable that logs completion with duration when disposed</returns>
    public static IDisposable TimeOperation(this ILogger logger, string operationName)
    {
        return new OperationTimer(logger, operationName);
    }

    /// <summary>
    /// Logs method entry with parameters
    /// </summary>
    public static void LogMethodEntry(
        this ILogger logger,
        object? parameters = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "")
    {
        var className = Path.GetFileNameWithoutExtension(filePath);
        
        if (parameters != null)
        {
            logger.Debug("→ Entering {ClassName}.{MethodName} with {@Parameters}", 
                className, methodName, parameters);
        }
        else
        {
            logger.Debug("→ Entering {ClassName}.{MethodName}", className, methodName);
        }
    }

    /// <summary>
    /// Logs method exit with optional result
    /// </summary>
    public static void LogMethodExit(
        this ILogger logger,
        object? result = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "")
    {
        var className = Path.GetFileNameWithoutExtension(filePath);
        
        if (result != null)
        {
            logger.Debug("← Exiting {ClassName}.{MethodName} with {@Result}", 
                className, methodName, result);
        }
        else
        {
            logger.Debug("← Exiting {ClassName}.{MethodName}", className, methodName);
        }
    }

    /// <summary>
    /// Logs an exception with structured context
    /// </summary>
    public static void LogException(
        this ILogger logger,
        Exception exception,
        string? context = null,
        Dictionary<string, object>? additionalData = null,
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "")
    {
        var className = Path.GetFileNameWithoutExtension(filePath);
        
        using var _ = BeginScope(new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["ClassName"] = className,
            ["MethodName"] = methodName
        });

        if (additionalData != null)
        {
            using var __ = BeginScope(additionalData);
            logger.Error(exception, "Exception in {ClassName}.{MethodName}: {Context}", 
                className, methodName, context ?? exception.Message);
        }
        else
        {
            logger.Error(exception, "Exception in {ClassName}.{MethodName}: {Context}", 
                className, methodName, context ?? exception.Message);
        }
    }

    /// <summary>
    /// Logs a performance metric
    /// </summary>
    public static void LogMetric(
        this ILogger logger,
        string metricName,
        double value,
        string? unit = null,
        Dictionary<string, object>? tags = null)
    {
        using var _ = BeginScope(new Dictionary<string, object>
        {
            ["MetricName"] = metricName,
            ["MetricValue"] = value,
            ["MetricUnit"] = unit ?? "count"
        });

        if (tags != null)
        {
            using var __ = BeginScope(tags);
            logger.Information("Metric: {MetricName} = {MetricValue} {MetricUnit}", 
                metricName, value, unit ?? "");
        }
        else
        {
            logger.Information("Metric: {MetricName} = {MetricValue} {MetricUnit}", 
                metricName, value, unit ?? "");
        }
    }

    /// <summary>
    /// Logs an event with structured properties
    /// </summary>
    public static void LogEvent(
        this ILogger logger,
        string eventName,
        Dictionary<string, object>? eventData = null)
    {
        using var _ = LogContext.PushProperty("EventName", eventName);
        
        if (eventData != null)
        {
            using var __ = BeginScope(eventData);
            logger.Information("Event: {EventName} {@EventData}", eventName, eventData);
        }
        else
        {
            logger.Information("Event: {EventName}", eventName);
        }
    }

    /// <summary>
    /// Logs a security-related event
    /// </summary>
    public static void LogSecurityEvent(
        this ILogger logger,
        string eventType,
        string? userId = null,
        string? ipAddress = null,
        string? details = null,
        bool success = true)
    {
        using var _ = BeginScope(new Dictionary<string, object>
        {
            ["SecurityEvent"] = true,
            ["SecurityEventType"] = eventType,
            ["SecuritySuccess"] = success
        });

        logger.Warning(
            "Security Event: {EventType} - User: {UserId} - IP: {IpAddress} - Success: {Success} - Details: {Details}",
            eventType, userId ?? "anonymous", ipAddress ?? "unknown", success, details ?? "");
    }

    /// <summary>
    /// Logs an API request/response
    /// </summary>
    public static void LogApiCall(
        this ILogger logger,
        string method,
        string path,
        int statusCode,
        long durationMs,
        string? userId = null,
        string? correlationId = null)
    {
        using var _ = BeginScope(new Dictionary<string, object>
        {
            ["ApiMethod"] = method,
            ["ApiPath"] = path,
            ["ApiStatusCode"] = statusCode,
            ["ApiDurationMs"] = durationMs
        });

        var level = statusCode >= 500 ? Serilog.Events.LogEventLevel.Error 
                  : statusCode >= 400 ? Serilog.Events.LogEventLevel.Warning 
                  : Serilog.Events.LogEventLevel.Information;

        logger.Write(level,
            "API {Method} {Path} responded {StatusCode} in {DurationMs}ms - User: {UserId} - CorrelationId: {CorrelationId}",
            method, path, statusCode, durationMs, userId ?? "anonymous", correlationId ?? "-");
    }

    private class OperationTimer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public OperationTimer(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
            _logger.Debug("Starting operation: {OperationName}", operationName);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.Information(
                "Completed operation: {OperationName} in {DurationMs}ms",
                _operationName, _stopwatch.ElapsedMilliseconds);
        }
    }

    private class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;

        public CompositeDisposable(List<IDisposable> disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}

