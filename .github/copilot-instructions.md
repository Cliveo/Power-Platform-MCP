# Copilot Instructions for this repo

## Your environment (quick-config)
Update these values to match your environment; the examples and defaults below will use them.

```yaml
quick_config:
  defaultOrgUrl: https://your-org.crm.dynamics.com/
  orgs:
    dev:  https://your-dev.crm.dynamics.com/
    uat:  https://your-uat.crm.dynamics.com/
    prod: https://your-prod.crm.dynamics.com/
  publisherPrefix: "xx" # your solution publisher prefix
  flowApiBaseUrl: "https://{region}.api.flow.microsoft.com" # e.g., australia, unitedstates
  # Find your environmentId from Dataverse: organizations.orgdborgsettings.ProjectHostEnvironmentId
  environmentId: "<set from orgdborgsettings.ProjectHostEnvironmentId>"
```

This repository contains a C# Model Context Protocol (MCP) server that runs via stdio and exposes a few tools.
Keep responses concise, concrete, and tailored to this project.

## Big picture
- Single .NET console app at `MyMCP/` using the MCP C# SDK (`ModelContextProtocol`).
- Transport: stdio (`WithStdioServerTransport`). The client (e.g., VS Code Copilot Agent) launches the server.
- Tools are discovered via attributes scanned from the running assembly (`WithToolsFromAssembly`).
- Current tool types:
  - `DataverseTools.*` as listed below.
- DI: `Microsoft.Extensions.Hosting` + `AddHttpClient()` + custom `DataverseClient`.

## Key files
- `MyMCP/Program.cs`: Host setup, logging, tool registration, and tool methods.
- `MyMCP/Services/DataverseClient.cs`: Dataverse Web API calls using `DefaultAzureCredential`.
- `MyMCP/Services/PowerAutomateClient.cs`: Power Automate Flow API calls using `DefaultAzureCredential`.
- `.vscode/mcp.json`: Configures how VS Code runs the MCP server.
- `README.md`: Quick usage and auth notes.

## Available Tools
- `DataverseTools.GetPluginTraceLogs(orgUrl, top, filter)` - Query plugin trace logs.
- `DataverseTools.Get(orgUrl, tableSetName, ...)` - Generic OData GET with full query support.
- `DataverseTools.ActivateWorkflow(orgUrl, workflowId, activate)` - Activate/deactivate workflows.
- `DataverseTools.GetEntityList(orgUrl)` - Get available entities (tables) metadata.
- `DataverseTools.GetEntityMetadata(orgUrl, entityLogicalName, includeAttributes, includeRelationships)` - Get detailed entity metadata.
- `PowerAutomateTools.GetFlowTriggers(flowApiBaseUrl, environmentId, flowId)` - List triggers for a flow.
- `PowerAutomateTools.GetManualTriggerCallbackUrl(flowApiBaseUrl, environmentId, flowId, triggerName)` - Get HTTP trigger callback URL for a trigger (defaults to `manual`).
- `PowerAutomateTools.GetFlowRuns(flowApiBaseUrl, environmentId, flowId)` - List flow run history.
- `PowerAutomateTools.GetFlowRunDetails(flowApiBaseUrl, environmentId, flowId, runName)` - Get a specific run's details.
- `PowerAutomateTools.GetFlowRunActions(flowApiBaseUrl, environmentId, flowId, runName)` - List actions for a run.
- `PowerAutomateTools.GetTriggerHistories(flowApiBaseUrl, environmentId, flowId, triggerName)` - List trigger firing history.

## Auth & external deps
- Azure identity via `Azure.Identity.DefaultAzureCredential`.
  - Local: relies on `az login` CLI session.
  - Cloud: Managed Identity recommended; grant Dataverse permissions.
- Dataverse Web API: `GET /api/data/v9.2/plugintracelogs` with OData params.
- NuGet packages: `ModelContextProtocol` (preview), `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging.Console`, `Microsoft.Extensions.Http`, `Azure.Identity`.
- Power Automate Flow API:
  - Token scope: `https://service.flow.microsoft.com/.default`.
  - Base URL is regional (e.g., `https://australia.api.flow.microsoft.com`).
  - Endpoints used:
    - `GET providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/triggers?api-version=2016-11-01`
    - `POST providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/triggers/{triggerName}/listCallbackUrl?api-version=2016-11-01`

## Build, run, debug
- Build: `dotnet build .\MyMCP\MyMCP.csproj -c Release`.
- Normal run is via Copilot Agent using `.vscode/mcp.json`. Avoid printing to stdout; logging is routed to stderr.
- Logging: configured via `builder.Logging.AddConsole(LogToStandardErrorThreshold = Trace)`.
- If debugging tools invocations, attach to the running process started by VS Code, or convert to a normal console run for breakpoints.
- Default org URL and publisher prefix come from the Quick-config above.

## Patterns & conventions
- Tools are static methods in classes marked `[McpServerToolType]`; methods marked `[McpServerTool]` and can use DI parameters.
- Tool return values are strings; JSON payloads are returned as raw JSON strings when appropriate.
- Dataverse client composes the OData query string; `filter` is passed through (ensure valid OData syntax).
- Keep stdout clean for MCP traffic; use logging for diagnostics.

## Example usages
- Logs: `MyMCP.GetPluginTraceLogs(orgUrl: "https://contoso.crm.dynamics.com", top: 10, filter: "messagename eq 'Create'")` → JSON.
- Entities: `MyMCP.GetEntityList(orgUrl: "https://contoso.crm.dynamics.com")` → JSON list of all entities.
- Metadata: `MyMCP.GetEntityMetadata(orgUrl: "https://contoso.crm.dynamics.com", entityLogicalName: "contact", includeAttributes: true, includeRelationships: false)` → JSON.
- Flow triggers: `MyMCP.GetFlowTriggers(flowApiBaseUrl: "https://australia.api.flow.microsoft.com", environmentId: "<envId>", flowId: "<flowId>")` → JSON triggers list.
- Flow manual URL: `MyMCP.GetManualTriggerCallbackUrl(flowApiBaseUrl: "https://australia.api.flow.microsoft.com", environmentId: "<envId>", flowId: "<flowId>")` → JSON with `response.value` containing the URL.
- Flow runs: `MyMCP.GetFlowRuns(flowApiBaseUrl: "https://australia.api.flow.microsoft.com", environmentId: "<envId>", flowId: "<flowId>")` → JSON runs list.
- Run details: `MyMCP.GetFlowRunDetails(flowApiBaseUrl: "https://australia.api.flow.microsoft.com", environmentId: "<envId>", flowId: "<flowId>", runName: "0858...")` → JSON run.
- Run actions: `MyMCP.GetFlowRunActions(flowApiBaseUrl: "https://australia.api.flow.microsoft.com", environmentId: "<envId>", flowId: "<flowId>", runName: "0858...")` → JSON actions.
- Trigger histories: `MyMCP.GetTriggerHistories(flowApiBaseUrl: "https://australia.api.flow.microsoft.com", environmentId: "<envId>", flowId: "<flowId>", triggerName: "manual")` → JSON histories.

## Extending
- Add new tools by creating a new static class with `[McpServerToolType]` and methods with `[McpServerTool]`.
- Inject services via method parameters; register them in `Program.cs` using DI.
- For additional Dataverse entities, add methods to `DataverseClient` and surface them via tools.

## Gotchas
- Ensure `az login` before Dataverse calls locally.
- Use the org-specific scope: `${orgUrl}/.default` when requesting tokens.
- If you change the project path, update `.vscode/mcp.json` args accordingly.
- Avoid writing to stdout; it breaks MCP protocol parsing.
- Note: the user table doesn't have statecode
- Environment ID vs organization ID: `organizationid` (Dataverse org GUID) is not the Power Platform environment ID. You can extract the environment ID from `organizations.orgdborgsettings` as `ProjectHostEnvironmentId`.

### Finding your Environment ID (Power Platform)
- Query organizations: `MyMCP.Get(orgUrl: "https://your-org.crm.dynamics.com/", tableSetName: "organizations", top: 1)`
- Inspect the returned JSON string and find `ProjectHostEnvironmentId` inside `orgdborgsettings`.
- Use that `environmentId` with the Flow tools.

## OData String Functions
- **DO NOT** use `tolower()` or `toupper()` functions - these are NOT supported and will cause query failures
- **DO** use these supported OData string functions:
  - `contains(fieldname,'searchterm')` - case insensitive by default
  - `startswith(fieldname,'prefix')` - case insensitive by default  
  - `endswith(fieldname,'suffix')` - case insensitive by default
- All string filtering in Dataverse is case insensitive by default, so no need for case conversion functions
- Example working queries:
  - `contains(fullname,'clive')` - finds users with "clive" anywhere in fullname
  - `startswith(fullname,'Clive')` - finds users whose fullname starts with "Clive"
  - `contains(fullname,'admin') or contains(domainname,'admin')` - multiple conditions

## Playbooks and query recipes (from recent sessions)

These are proven, copy-ready patterns for common Dataverse tasks using the `MyMCP` tools. Defaults come from the Quick-config at the top of this file.


Constants and mappings
- Cloud Flow category: `workflows.category = 5` (Modern Flow)
- Workflow state: `statecode` → 0 = Draft (inactive), 1 = Activated
- Solution Components: Workflows are `componenttype = 29`
- Filtering solutioncomponents by solution requires the lookup column: `_solutionid_value`

1) List inactive cloud flows
- Tool: `MyMCP.Get`
- Table: `workflows`
- Select: `name,workflowid,statecode,statuscode,category,createdon,modifiedon,ownerid`
- Filter: `category eq 5 and statecode eq 0`
- Order: `modifiedon desc`
- Example: MyMCP.Get(orgUrl: "https://your-org.crm.dynamics.com/", tableSetName: "workflows", select: "name,workflowid,statecode,statuscode,category,createdon,modifiedon,ownerid", filter: "category eq 5 and statecode eq 0", orderby: "modifiedon desc", top: 25)

2) Find cloud flows in solutions where publisher prefix is "co" (COMPLETE WORKFLOW)
- Step A: Find solutions with that publisher prefix
  - Table: `solutions`
  - Expand: `publisherid($select=customizationprefix,uniquename,friendlyname)`
  - Filter: `publisherid/customizationprefix eq 'co'`
  - Example: MyMCP.Get(orgUrl: "https://your-org.crm.dynamics.com/", tableSetName: "solutions", select: "solutionid,friendlyname,uniquename,publisherid", expand: "publisherid($select=customizationprefix,uniquename,friendlyname)", filter: "publisherid/customizationprefix eq 'xx'", top: 50)
- Step B: For all solution IDs, list workflow components (optimized single query)
  - Table: `solutioncomponents`
  - Filter: `(_solutionid_value eq <id1> or _solutionid_value eq <id2> or ...) and componenttype eq 29`
  - Select: `objectid,componenttype,solutioncomponentid,_solutionid_value`
  - Note: Use `_solutionid_value` (not `solutionid`) to filter the lookup.
  - Example: MyMCP.Get(..., tableSetName: "solutioncomponents", filter: "(_solutionid_value eq bed321d4-ffd1-ef11-a72f-002248101cfe or _solutionid_value eq 195efb3a-46d2-ef11-a72f-002248101cfe or _solutionid_value eq a637a36e-47d2-ef11-a72f-002248101cfe or _solutionid_value eq eb5bb7c2-3392-ef11-8a69-00224895daf0) and componenttype eq 29", select: "objectid,componenttype,solutioncomponentid,_solutionid_value", top: 100)
- Step C: Fetch the workflows by the object IDs from Step B
  - Table: `workflows`
  - Filter: `category eq 5 and (workflowid eq <id1> or workflowid eq <id2> or ...)`
  - Select: `name,workflowid,category,statecode,statuscode,modifiedon,ownerid,createdon`
  - Note: Don't use expand for owner - the principal table doesn't have 'fullname' property
  - Example: MyMCP.Get(..., tableSetName: "workflows", filter: "category eq 5 and (workflowid eq 93eec778-97cc-ef11-a72f-002248101cfe or workflowid eq d2950668-d743-f011-877a-0022481048fb or workflowid eq 1def5171-64a4-ef11-a72f-002248123766 or workflowid eq b3b57cc5-1d98-ef11-8a6a-00224895daf0)", select: "name,workflowid,category,statecode,statuscode,modifiedon,ownerid,createdon", top: 50)

Why not a single subquery? The Dataverse OData endpoint is limited for cross-entity subqueries; split into the above steps for reliability.

When listing flows - display them as a markdown table with status icons (✅ for Activated, ⏸️ for Draft).

3) Activate/Deactivate a workflow (cloud flow)
- Tool: `MyMCP.ActivateWorkflow(orgUrl, workflowId, activate)`
- Activate: `MyMCP.ActivateWorkflow(orgUrl, workflowId, activate: true)` (default)
- Deactivate: `MyMCP.ActivateWorkflow(orgUrl, workflowId, activate: false)`
- Behavior: Uses `SetState` with `EntityMoniker` `@odata.type=Microsoft.Dynamics.CRM.workflow`
  - Activate: `State=1`, `Status=2` (Activated)
  - Deactivate: `State=0`, `Status=1` (Draft)
- Example: MyMCP.ActivateWorkflow(orgUrl: "https://your-org.crm.dynamics.com/", workflowId: "<workflow-guid>", activate: false)

4) Get plugin trace logs
- Tool: `MyMCP.GetPluginTraceLogs(orgUrl, top, filter)`
- Example: `filter: messagename eq 'Create'`

5) Generic data reads (examples)
- Contacts sample: `MyMCP.Get(..., tableSetName: "contacts", select: "fullname,contactid", top: 5)`
- Include formatted values: already enabled via Prefer/Accept headers in the Dataverse client.
- Owner names: add `expand: "ownerid($select=fullname)"`

Troubleshooting tips
- If a query fails, reduce complexity (remove expand/orderby) and verify the table and columns.
- For solutioncomponents, start unfiltered with a small `top` to confirm shape, then add filters.
- Use correct GUID literals (no braces) and the lookup field names (e.g., `_solutionid_value`).
