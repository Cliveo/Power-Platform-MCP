# MyMCP

This MCP server exposes:
- Dataverse tools using Azure AD auth via DefaultAzureCredential
- Power Automate tools using Azure AD auth via DefaultAzureCredential

## Prereqs
- .NET 10 SDK
- Azure CLI logged in: `az login`
- Access to Dataverse environment and permission to read `plugintracelogs`
 

## VS Code MCP
Before using the tools, set your environment in `.github/copilot-instructions.md` under "Your environment (quick-config)" (org URLs, publisher prefix, flow API base URL, environmentId). Replace placeholders like `https://your-org.crm.dynamics.com/`.

The server is registered in `.vscode/mcp.json` select `Start`. In GitHub Copilot Agent mode, you can call tools like:

### Dataverse Tools
- MyMCP.GetPluginTraceLogs
- MyMCP.Get (generic OData GET)
- MyMCP.ActivateWorkflow
- MyMCP.GetEntityList
- MyMCP.GetEntityMetadata

### Power Automate Tools
- MyMCP.GetFlowTriggers
- MyMCP.GetManualTriggerCallbackUrl
- MyMCP.GetFlowRuns
- MyMCP.GetFlowRunDetails
- MyMCP.GetFlowRunActions
- MyMCP.GetTriggerHistories

## Tool Parameters

### GetPluginTraceLogs
- orgUrl (required): e.g. https://contoso.crm.dynamics.com
- top (optional, default 25): max log records
- filter (optional): OData filter, e.g. `messagename eq 'Create'`

### GetFlowTriggers
- flowApiBaseUrl (required): e.g. https://{region}.api.flow.microsoft.com
- environmentId (required): Environment ID (GUID or name)
- flowId (required): Flow GUID

### GetManualTriggerCallbackUrl
- flowApiBaseUrl (required): e.g. https://{region}.api.flow.microsoft.com
- environmentId (required): Environment ID (GUID or name)
- flowId (required): Flow GUID
- triggerName (optional, default 'manual'): Trigger name

## Local run
Copilot Agent launches the server from `.vscode/mcp.json`. For manual build:

```powershell
 dotnet build .\MyMCP\MyMCP.csproj -c Release
```

## Auth notes
- Locally: DefaultAzureCredential uses Azure CLI token from `az login`.
