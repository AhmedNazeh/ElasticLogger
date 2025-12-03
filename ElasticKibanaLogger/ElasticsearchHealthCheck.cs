using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http;
using System.Text.Json;

namespace Nazeh.ElasticLogger;

/// <summary>
/// Health check for Elasticsearch connectivity
/// </summary>
public class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly ElasticLoggerSettings _settings;
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Creates a new Elasticsearch health check
    /// </summary>
    /// <param name="settings">Logger settings containing ES configuration</param>
    public ElasticsearchHealthCheck(ElasticLoggerSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Checks the health of the Elasticsearch connection
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(new Uri(_settings.Elasticsearch.Uri), "_cluster/health");
            
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            
            // Add authentication
            switch (_settings.Elasticsearch.AuthMethod)
            {
                case ElasticsearchAuthMethod.Basic:
                    var basicAuth = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes(
                            $"{_settings.Elasticsearch.Username}:{_settings.Elasticsearch.Password}"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
                    break;
                
                case ElasticsearchAuthMethod.ApiKey:
                    string apiKey;
                    if (!string.IsNullOrEmpty(_settings.Elasticsearch.EncodedApiKey))
                    {
                        apiKey = _settings.Elasticsearch.EncodedApiKey;
                    }
                    else
                    {
                        apiKey = Convert.ToBase64String(
                            System.Text.Encoding.ASCII.GetBytes(
                                $"{_settings.Elasticsearch.ApiKeyId}:{_settings.Elasticsearch.ApiKeySecret}"));
                    }
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ApiKey", apiKey);
                    break;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_settings.HealthCheck.TimeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy(
                    $"Elasticsearch returned {response.StatusCode}",
                    data: new Dictionary<string, object>
                    {
                        ["StatusCode"] = (int)response.StatusCode,
                        ["Uri"] = _settings.Elasticsearch.Uri
                    });
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var healthResponse = JsonSerializer.Deserialize<ElasticsearchHealthResponse>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var data = new Dictionary<string, object>
            {
                ["ClusterName"] = healthResponse?.ClusterName ?? "unknown",
                ["Status"] = healthResponse?.Status ?? "unknown",
                ["NumberOfNodes"] = healthResponse?.NumberOfNodes ?? 0,
                ["ActiveShards"] = healthResponse?.ActiveShards ?? 0,
                ["Uri"] = _settings.Elasticsearch.Uri
            };

            return healthResponse?.Status?.ToLower() switch
            {
                "green" => HealthCheckResult.Healthy("Elasticsearch cluster is healthy", data),
                "yellow" => HealthCheckResult.Degraded("Elasticsearch cluster is in yellow state", data: data),
                "red" => HealthCheckResult.Unhealthy("Elasticsearch cluster is in red state", data: data),
                _ => HealthCheckResult.Unhealthy($"Unknown cluster status: {healthResponse?.Status}", data: data)
            };
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"Elasticsearch health check timed out after {_settings.HealthCheck.TimeoutSeconds}s",
                data: new Dictionary<string, object> { ["Uri"] = _settings.Elasticsearch.Uri });
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Failed to connect to Elasticsearch: {ex.Message}",
                ex,
                new Dictionary<string, object> { ["Uri"] = _settings.Elasticsearch.Uri });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Unexpected error checking Elasticsearch health: {ex.Message}",
                ex,
                new Dictionary<string, object> { ["Uri"] = _settings.Elasticsearch.Uri });
        }
    }

    private class ElasticsearchHealthResponse
    {
        public string? ClusterName { get; set; }
        public string? Status { get; set; }
        public int NumberOfNodes { get; set; }
        public int ActiveShards { get; set; }
        public int ActivePrimaryShards { get; set; }
        public int RelocatingShards { get; set; }
        public int InitializingShards { get; set; }
        public int UnassignedShards { get; set; }
    }
}

