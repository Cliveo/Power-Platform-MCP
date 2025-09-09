using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Azure.Core;
using Azure.Identity;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyMCP.Services;

public class DataverseClient
{
    private readonly HttpClient _http;
    private readonly DefaultAzureCredential _credential;
    private readonly ILogger<DataverseClient> _logger;

    public DataverseClient(IHttpClientFactory httpClientFactory, ILogger<DataverseClient> logger)
    {
        _http = httpClientFactory.CreateClient();
        _logger = logger;
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });
    }

    public async Task<JsonDocument> GetPluginTraceLogsAsync(string orgUrl, int top = 25, string? filter = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orgUrl))
            throw new ArgumentException("Dataverse org URL must be provided, e.g. https://contoso.crm.dynamics.com", nameof(orgUrl));

        var scope = $"{orgUrl.TrimEnd('/')}/.default";
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);

        var query = $"$select=messagename,typename,exceptiondetails,performanceexecutionduration,createdon,correlationid,operationtype&$orderby=createdon desc&$top={top}";
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query += $"&$filter={Uri.EscapeDataString(filter)}";
        }

        var url = new Uri(new Uri(orgUrl), $"/api/data/v9.2/plugintracelogs?{query}");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Dataverse GET FAILED: url={Url} status={StatusCode} reason={ReasonPhrase} body={Body}",
                url,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try { return JsonDocument.Parse(errorBody); } catch { /* wrap below */ }
            }

            var fallback = new
            {
                error = new
                {
                    code = (int)response.StatusCode,
                    message = response.ReasonPhrase,
                    details = errorBody
                }
            };
            var fallbackJson = JsonSerializer.Serialize(fallback);
            return JsonDocument.Parse(fallbackJson);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return json;
    }

    public async Task SetWorkflowActivationAsync(string orgUrl, Guid workflowId, bool activate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orgUrl))
            throw new ArgumentException("Dataverse org URL must be provided, e.g. https://contoso.crm.dynamics.com", nameof(orgUrl));
        if (workflowId == Guid.Empty)
            throw new ArgumentException("A valid workflowId GUID is required.", nameof(workflowId));

        var scope = $"{orgUrl.TrimEnd('/')}/.default";
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);

        var (state, status) = activate ? (1, 2) : (0, 1);

        var url = new Uri(new Uri(orgUrl), $"/api/data/v9.2/workflows({workflowId})");
        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var payload = new Dictionary<string, object?>
        {
            ["statecode"] = state,
            ["statuscode"] = status
        };
        var payloadJson = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

        _logger.LogInformation("SetWorkflowActivation (PATCH): workflowId={WorkflowId}, activate={Activate}, url={Url}", workflowId, activate, url);
        _logger.LogDebug("SetWorkflowActivation PATCH payload: {Payload}", payloadJson);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "SetWorkflowActivation (PATCH) FAILED: status={StatusCode} reason={ReasonPhrase} body={Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);
            throw new HttpRequestException($"SetWorkflowActivation (PATCH) failed with status {(int)response.StatusCode}: {response.ReasonPhrase}. Body: {errorBody}");
        }
        else
        {
            _logger.LogInformation("SetWorkflowActivation (PATCH) succeeded for workflowId={WorkflowId} (activate={Activate})", workflowId, activate);
        }
    }

    public Task ActivateWorkflowAsync(string orgUrl, Guid workflowId, CancellationToken cancellationToken = default)
        => SetWorkflowActivationAsync(orgUrl, workflowId, activate: true, cancellationToken);


    public async Task<JsonDocument> GetAsync(
        string orgUrl,
        string tableSetName,
        string? select = null,
        string? filter = null,
        int? top = null,
        string? expand = null,
        string? orderby = null,
        string? apply = null,
        bool count = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orgUrl))
            throw new ArgumentException("Dataverse org URL must be provided, e.g. https://contoso.crm.dynamics.com", nameof(orgUrl));
        if (string.IsNullOrWhiteSpace(tableSetName))
            throw new ArgumentException("Table set name must be provided, e.g. contacts or plugintracelogs", nameof(tableSetName));

        var scope = $"{orgUrl.TrimEnd('/')}/.default";
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);

        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(select)) q.Add("$select=" + Uri.EscapeDataString(select));
        if (!string.IsNullOrWhiteSpace(filter)) q.Add("$filter=" + Uri.EscapeDataString(filter));
        if (top.HasValue) q.Add("$top=" + top.Value);
        if (!string.IsNullOrWhiteSpace(expand)) q.Add("$expand=" + Uri.EscapeDataString(expand));
        if (!string.IsNullOrWhiteSpace(orderby)) q.Add("$orderby=" + Uri.EscapeDataString(orderby));
        if (!string.IsNullOrWhiteSpace(apply)) q.Add("$apply=" + Uri.EscapeDataString(apply));
        if (count) q.Add("$count=true");

        var qs = q.Count > 0 ? ("?" + string.Join("&", q)) : string.Empty;
        var url = new Uri(new Uri(orgUrl), $"/api/data/v9.2/{tableSetName}{qs}");

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Accept", "application/json;odata.metadata=full");
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");
        request.Headers.TryAddWithoutValidation("Prefer", "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue,Microsoft.Dynamics.CRM.associatednavigationproperty,Microsoft.Dynamics.CRM.lookuplogicalname\"");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Dataverse GET FAILED: url={Url} status={StatusCode} reason={ReasonPhrase} body={Body}",
                url,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try { return JsonDocument.Parse(errorBody); } catch { /* wrap below */ }
            }

            var fallback = new
            {
                error = new
                {
                    code = (int)response.StatusCode,
                    message = response.ReasonPhrase,
                    details = errorBody
                }
            };
            var fallbackJson = JsonSerializer.Serialize(fallback);
            return JsonDocument.Parse(fallbackJson);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return json;
    }

    public async Task<JsonDocument> GetEntityListAsync(
        string orgUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orgUrl))
            throw new ArgumentException("Dataverse org URL must be provided, e.g. https://contoso.crm.dynamics.com", nameof(orgUrl));

        var scope = $"{orgUrl.TrimEnd('/')}/.default";
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);

        var url = new Uri(new Uri(orgUrl), "/api/data/v9.2/EntityDefinitions");

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Dataverse GET EntityDefinitions FAILED: url={Url} status={StatusCode} reason={ReasonPhrase} body={Body}",
                url,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try { return JsonDocument.Parse(errorBody); } catch { /* wrap below */ }
            }

            var fallback = new
            {
                error = new
                {
                    code = (int)response.StatusCode,
                    message = response.ReasonPhrase,
                    details = errorBody
                }
            };
            var fallbackJson = JsonSerializer.Serialize(fallback);
            return JsonDocument.Parse(fallbackJson);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return json;
    }

    public async Task<JsonDocument> GetEntityMetadataAsync(
        string orgUrl,
        string entityLogicalName,
        bool includeAttributes = true,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orgUrl))
            throw new ArgumentException("Dataverse org URL must be provided, e.g. https://contoso.crm.dynamics.com", nameof(orgUrl));
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name must be provided, e.g. contact or account", nameof(entityLogicalName));

        var scope = $"{orgUrl.TrimEnd('/')}/.default";
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);

        var q = new List<string>();
        var expandParts = new List<string>();
        
        if (includeAttributes)
            expandParts.Add("Attributes");
        if (includeRelationships)
        {
            expandParts.Add("OneToManyRelationships");
            expandParts.Add("ManyToOneRelationships");
            expandParts.Add("ManyToManyRelationships");
        }
        
        if (expandParts.Count > 0)
            q.Add("$expand=" + Uri.EscapeDataString(string.Join(",", expandParts)));

        var qs = q.Count > 0 ? ("?" + string.Join("&", q)) : string.Empty;
        var url = new Uri(new Uri(orgUrl), $"/api/data/v9.2/EntityDefinitions(LogicalName='{entityLogicalName}'){qs}");

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Dataverse GET EntityDefinition FAILED: url={Url} status={StatusCode} reason={ReasonPhrase} body={Body}",
                url,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                try { return JsonDocument.Parse(errorBody); } catch { /* wrap below */ }
            }

            var fallback = new
            {
                error = new
                {
                    code = (int)response.StatusCode,
                    message = response.ReasonPhrase,
                    details = errorBody
                }
            };
            var fallbackJson = JsonSerializer.Serialize(fallback);
            return JsonDocument.Parse(fallbackJson);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return json;
    }
}
