using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using MyMCP.Services;
using System.Text.Json;
using System;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
	consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddHttpClient();

builder.Services
	.AddMcpServer()
	.WithStdioServerTransport()
	.WithToolsFromAssembly();

builder.Services.AddSingleton<DataverseClient>();
builder.Services.AddSingleton<PowerAutomateClient>();

await builder.Build().RunAsync();

// Echo tools removed

[McpServerToolType]
public static class DataverseTools
{
	[McpServerTool, Description("Query Dataverse plugin trace logs using Managed Identity/az login.")]
	public static async Task<string> GetPluginTraceLogs(
		DataverseClient client,
		[Description("Dataverse org URL, e.g. https://contoso.crm.dynamics.com")] string orgUrl,
		[Description("Max records to return (default 25)")] int top = 25,
		[Description("Optional OData filter, e.g. messagename eq 'Create'")] string? filter = null,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetPluginTraceLogsAsync(orgUrl, top, filter, cancellationToken);
		// Return raw JSON string from Dataverse
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("Generic Dataverse OData GET with formatted values. Returns raw JSON.")]
	public static async Task<string> Get(
		DataverseClient client,
		[Description("Dataverse org URL, e.g. https://contoso.crm.dynamics.com")] string orgUrl,
		[Description("Table set name, e.g. contacts, accounts, plugintracelogs")] string tableSetName,
		[Description("$select clause, comma-separated")]
		string? select = null,
		[Description("$filter OData filter expression")]
		string? filter = null,
		[Description("$top max records")]
		int? top = null,
		[Description("$expand related entities")]
		string? expand = null,
		[Description("$orderby clause")]
		string? orderby = null,
		[Description("$apply transformation, e.g. groupby or aggregate")]
		string? apply = null,
		[Description("Include $count=true")]
		bool count = false,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetAsync(orgUrl, tableSetName, select, filter, top, expand, orderby, apply, count, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("Activate or deactivate a workflow using SetState. When activate=true -> state=1/status=2; when false -> state=0/status=1.")]
	public static async Task<string> ActivateWorkflow(
		DataverseClient client,
		[Description("Dataverse org URL, e.g. https://contoso.crm.dynamics.com")] string orgUrl,
		[Description("workflowId GUID to activate/deactivate")] string workflowId,
		[Description("true to activate, false to deactivate")] bool activate = true,
		CancellationToken cancellationToken = default)
	{
		if (!Guid.TryParse(workflowId, out var wfId))
		{
			return "ERROR: workflowId must be a valid GUID";
		}
		try
		{
			await client.SetWorkflowActivationAsync(orgUrl, wfId, activate, cancellationToken);
			return $"{(activate ? "Activated" : "Deactivated")} workflow {workflowId}";
		}
		catch (Exception ex)
		{
			return $"ERROR calling SetWorkflowActivation: {ex.Message}";
		}
	}

	[McpServerTool, Description("Get list of available entities (tables) in Dataverse. Returns EntityDefinitions metadata.")]
	public static async Task<string> GetEntityList(
		DataverseClient client,
		[Description("Dataverse org URL, e.g. https://contoso.crm.dynamics.com")] string orgUrl,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetEntityListAsync(orgUrl, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("Get detailed metadata for a specific entity including attributes and relationships.")]
	public static async Task<string> GetEntityMetadata(
		DataverseClient client,
		[Description("Dataverse org URL, e.g. https://contoso.crm.dynamics.com")] string orgUrl,
		[Description("Entity logical name, e.g. contact, account, opportunity")] string entityLogicalName,
		[Description("Include attribute definitions (default true)")] bool includeAttributes = true,
		[Description("Include relationship definitions (default false)")] bool includeRelationships = false,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetEntityMetadataAsync(orgUrl, entityLogicalName, includeAttributes, includeRelationships, cancellationToken);
		return doc.RootElement.GetRawText();
	}
}

// Azure DevOps tools removed

[McpServerToolType]
public static class PowerAutomateTools
{
	[McpServerTool, Description("List triggers for a Power Automate flow.")]
	public static async Task<string> GetFlowTriggers(
		PowerAutomateClient client,
		[Description("Flow API base URL, e.g. https://australia.api.flow.microsoft.com")] string flowApiBaseUrl,
		[Description("Environment ID (GUID or name) e.g. Default-xxxx")] string environmentId,
		[Description("Flow ID (GUID)")] string flowId,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetTriggersAsync(flowApiBaseUrl, environmentId, flowId, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("Get the callback URL for a flow's manual (HTTP) trigger.")]
	public static async Task<string> GetManualTriggerCallbackUrl(
		PowerAutomateClient client,
		[Description("Flow API base URL, e.g. https://australia.api.flow.microsoft.com")] string flowApiBaseUrl,
		[Description("Environment ID (GUID or name) e.g. Default-xxxx")] string environmentId,
		[Description("Flow ID (GUID)")] string flowId,
		[Description("Trigger name (default 'manual')")] string triggerName = "manual",
		CancellationToken cancellationToken = default)
	{
		var doc = await client.ListManualTriggerCallbackUrlAsync(flowApiBaseUrl, environmentId, flowId, triggerName, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("List runs (execution history) for a Power Automate flow.")]
	public static async Task<string> GetFlowRuns(
		PowerAutomateClient client,
		[Description("Flow API base URL, e.g. https://australia.api.flow.microsoft.com")] string flowApiBaseUrl,
		[Description("Environment ID (GUID or name) e.g. Default-xxxx")] string environmentId,
		[Description("Flow ID (GUID)")] string flowId,
		[Description("Optional UTC lower bound. Include runs with StartTime greater than this (e.g., 2025-09-08T00:00:00Z).")] string? since = null,
		[Description("Optional status filter: Succeeded, Failed, Canceled, Running, or All (default)." )] string? status = null,
		CancellationToken cancellationToken = default)
	{
		DateTimeOffset? sinceDto = null;
		if (!string.IsNullOrWhiteSpace(since))
		{
			// Accept ISO 8601 with Z; if missing Z, assume as UTC
			if (DateTimeOffset.TryParse(since, out var parsed))
			{
				sinceDto = parsed.ToUniversalTime();
			}
			else
			{
				return "ERROR: since must be ISO 8601 (e.g., 2025-09-08T00:00:00Z)";
			}
		}

		var doc = await client.GetFlowRunsAsync(flowApiBaseUrl, environmentId, flowId, sinceDto, status, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("Get details of a specific flow run.")]
	public static async Task<string> GetFlowRunDetails(
		PowerAutomateClient client,
		[Description("Flow API base URL, e.g. https://australia.api.flow.microsoft.com")] string flowApiBaseUrl,
		[Description("Environment ID (GUID or name) e.g. Default-xxxx")] string environmentId,
		[Description("Flow ID (GUID)")] string flowId,
		[Description("Run name (from runs list)")] string runName,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetFlowRunDetailsAsync(flowApiBaseUrl, environmentId, flowId, runName, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("List actions (steps) of a specific flow run.")]
	public static async Task<string> GetFlowRunActions(
		PowerAutomateClient client,
		[Description("Flow API base URL, e.g. https://australia.api.flow.microsoft.com")] string flowApiBaseUrl,
		[Description("Environment ID (GUID or name) e.g. Default-xxxx")] string environmentId,
		[Description("Flow ID (GUID)")] string flowId,
		[Description("Run name (from runs list)")] string runName,
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetFlowRunActionsAsync(flowApiBaseUrl, environmentId, flowId, runName, cancellationToken);
		return doc.RootElement.GetRawText();
	}

	[McpServerTool, Description("List trigger histories for a flow's trigger.")]
	public static async Task<string> GetTriggerHistories(
		PowerAutomateClient client,
		[Description("Flow API base URL, e.g. https://australia.api.flow.microsoft.com")] string flowApiBaseUrl,
		[Description("Environment ID (GUID or name) e.g. Default-xxxx")] string environmentId,
		[Description("Flow ID (GUID)")] string flowId,
		[Description("Trigger name (default 'manual')")] string triggerName = "manual",
		CancellationToken cancellationToken = default)
	{
		var doc = await client.GetTriggerHistoriesAsync(flowApiBaseUrl, environmentId, flowId, triggerName, cancellationToken);
		return doc.RootElement.GetRawText();
	}
}
