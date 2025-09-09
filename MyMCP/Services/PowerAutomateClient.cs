using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace MyMCP.Services;

public class PowerAutomateClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly ILogger<PowerAutomateClient> _logger;

    private static readonly string[] FlowScopes = new[] { "https://service.flow.microsoft.com/.default" };

    public PowerAutomateClient(HttpClient httpClient, ILogger<PowerAutomateClient> logger)
    {
        _httpClient = httpClient;
        _credential = new DefaultAzureCredential();
        _logger = logger;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(FlowScopes), cancellationToken);
        return token.Token;
    }

    private static string CombineUrl(string baseUrl, string path)
        => baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

    public async Task<JsonDocument> GetTriggersAsync(
        string flowApiBaseUrl,
        string environmentId,
        string flowId,
        CancellationToken cancellationToken = default)
    {
        var url = CombineUrl(flowApiBaseUrl, $"providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/triggers?api-version=2016-11-01");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task<JsonDocument> ListManualTriggerCallbackUrlAsync(
        string flowApiBaseUrl,
        string environmentId,
        string flowId,
        string triggerName = "manual",
        CancellationToken cancellationToken = default)
    {
        var url = CombineUrl(flowApiBaseUrl, $"providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/triggers/{triggerName}/listCallbackUrl?api-version=2016-11-01");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task<JsonDocument> GetFlowRunsAsync(
        string flowApiBaseUrl,
        string environmentId,
        string flowId,
        DateTimeOffset? since = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var basePath = $"providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/runs";

        // Build query with optional $filter for StartTime and Status
        var queryParams = new List<string> { "api-version=2016-11-01" };

        var filterParts = new List<string>();
        if (since.HasValue)
        {
            // API expects ISO 8601 (UTC) like 2025-09-08T00:00:00Z
            var iso = since.Value.UtcDateTime.ToString("s") + "Z";
            filterParts.Add($"StartTime gt {iso}");
        }
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            filterParts.Add($"Status eq '{status}'");
        }
        if (filterParts.Count > 0)
        {
            var filter = string.Join(" and ", filterParts);
            queryParams.Add("$filter=" + Uri.EscapeDataString(filter));
        }

        // Request a reasonable page size
        queryParams.Add("$top=250");

        var url = CombineUrl(flowApiBaseUrl, basePath) + "?" + string.Join("&", queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task<JsonDocument> GetFlowRunDetailsAsync(
        string flowApiBaseUrl,
        string environmentId,
        string flowId,
        string runName,
        CancellationToken cancellationToken = default)
    {
        var url = CombineUrl(flowApiBaseUrl, $"providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/runs/{runName}?api-version=2016-11-01");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task<JsonDocument> GetFlowRunActionsAsync(
        string flowApiBaseUrl,
        string environmentId,
        string flowId,
        string runName,
        CancellationToken cancellationToken = default)
    {
        var url = CombineUrl(flowApiBaseUrl, $"providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/runs/{runName}/actions?api-version=2016-11-01");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task<JsonDocument> GetTriggerHistoriesAsync(
        string flowApiBaseUrl,
        string environmentId,
        string flowId,
        string triggerName = "manual",
        CancellationToken cancellationToken = default)
    {
        var url = CombineUrl(flowApiBaseUrl, $"providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/triggers/{triggerName}/histories?api-version=2016-11-01");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
