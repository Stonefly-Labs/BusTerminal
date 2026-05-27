# `monitoring` module

Provisions the env-scoped observability backbone: a Log Analytics Workspace (LAW) and a workspace-based Application Insights component, plus an optional Key Vault secret materializing the App Insights connection string for downstream Container Apps secret references.

- LAW: `Azure/avm-res-operationalinsights-workspace/azurerm` pinned to `0.4.2`.
- App Insights: `Azure/avm-res-insights-component/azurerm` pinned to `0.3.0`.

## Inputs

| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `log_analytics_workspace_name` | string | yes | n/a | LAW resource name. |
| `application_insights_name` | string | yes | n/a | App Insights component name. |
| `resource_group_name` | string | yes | n/a | RG hosting both resources. |
| `location` | string | yes | n/a | Azure region. |
| `retention_in_days` | number | no | `30` | LAW retention. Spec 005 / Q5c default; valid range 30–730. |
| `key_vault_id` | string | no | `null` | When set, an `azurerm_key_vault_secret` named `ApplicationInsightsConnectionString` is created in this KV. Set to `null` to skip (when the env composition wants to materialize the secret itself to break a cycle). |
| `local_authentication_disabled` | bool | no | `false` | See § Local authentication. |
| `tags` | map(string) | no | `{}` | Applied to both resources. |

## Outputs

| Name | Description |
|---|---|
| `log_analytics_workspace_id` | LAW resource ID — pass to `diagnostic-settings` module sinks. |
| `log_analytics_workspace_customer_id` | LAW workspace GUID. |
| `application_insights_id` | App Insights resource ID — scope for `Monitoring Metrics Publisher` grants. |
| `application_insights_app_id` | App Insights `app_id` GUID (REST query API). |
| `application_insights_name` | Echo of `var.application_insights_name`. |
| `application_insights_connection_string` | Sensitive. Prefer consuming via the KV-secret URI. |
| `app_insights_connection_string_secret_uri` | KV secret URI (`null` when `key_vault_id` is `null`). |

## Local authentication

`local_authentication_disabled` controls whether the App Insights resource accepts ingestion authenticated by the connection-string ingestion key vs. Microsoft Entra. **This MUST remain `false`** in every BusTerminal environment.

Reason (spec 005 / Q1c / research §6):

- The Application Insights JavaScript SDK does **not** support Microsoft Entra ingestion authentication — Microsoft Learn lists it as an unsupported scenario: <https://learn.microsoft.com/azure/azure-monitor/app/azure-ad-authentication#unsupported-scenarios>.
- Setting `local_authentication_disabled = true` would silently break all browser telemetry (the frontend's `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` ingestion path), with no client-visible error.
- The backend .NET OpenTelemetry exporter authenticates to ingestion via AAD using `APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=<workload-uami-client-id>"`. That env var works **alongside** the connection string, not as a replacement for it, and requires `local_authentication_disabled = false` on the resource. The exporter still needs the connection string to identify the target App Insights component; the AAD auth string supplies the credential.
- The workload UAMI is granted `Monitoring Metrics Publisher` (built-in role GUID `3913510d-42f4-4e42-8a64-420c390055eb`) scoped to the App Insights resource via the env composition's `module.workload_identity.assigned_azure_rbac` map.

Net result: **backend telemetry flows via AAD** (zero credentials in the request); **browser telemetry continues to flow via the connection-string ingestion key**, surfaced through KV and Container Apps secret references so it never lands in source or state. Both paths require the resource to accept local auth, hence the constraint.

## FR-047 compliance — telemetry payloads

The App Insights resource ingests application telemetry, which can contain PII if the application emits it (e.g., a request log that includes a query string with a user identifier). PII-suppression is the responsibility of the OpenTelemetry exporter configuration in the application code, not this module. This module is concerned only with provisioning the ingest endpoint and the connection-string secret materialization.

## Usage

```hcl
module "monitoring" {
  source = "../../modules/monitoring"

  log_analytics_workspace_name = module.naming.log_analytics_workspace_name
  application_insights_name    = module.naming.application_insights_name
  resource_group_name          = azurerm_resource_group.this.name
  location                     = azurerm_resource_group.this.location

  retention_in_days             = var.log_analytics_retention_days
  local_authentication_disabled = false # see § Local authentication
  key_vault_id                  = null  # composition handles KV secret to break the LAW → KV → secret cycle

  tags = local.shared_tags
}
```
