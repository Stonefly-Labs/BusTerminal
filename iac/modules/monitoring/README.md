# `monitoring` module

Provisions the env-scoped observability backbone: a Log Analytics Workspace (LAW) and a workspace-based Application Insights component, plus an optional Key Vault secret materializing the App Insights connection string for downstream Container Apps secret references.

- LAW: `Azure/avm-res-operationalinsights-workspace/azurerm` pinned to `0.4.2`.
- App Insights: `Azure/avm-res-insights-component/azurerm` pinned to `0.3.0`.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_key_vault_secret.app_insights_connection_string](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/key_vault_secret) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_application_insights_name"></a> [application\_insights\_name](#input\_application\_insights\_name) | Name of the Application Insights component. | `string` | n/a | yes |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the monitoring resources. | `string` | n/a | yes |
| <a name="input_log_analytics_workspace_name"></a> [log\_analytics\_workspace\_name](#input\_log\_analytics\_workspace\_name) | Name of the Log Analytics Workspace. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting both monitoring resources. | `string` | n/a | yes |
| <a name="input_key_vault_id"></a> [key\_vault\_id](#input\_key\_vault\_id) | Optional Key Vault ID where the Application Insights connection string is exposed as a secret for workload consumption. Set to null to skip secret creation. | `string` | `null` | no |
| <a name="input_local_authentication_disabled"></a> [local\_authentication\_disabled](#input\_local\_authentication\_disabled) | Forwarded to `azurerm_application_insights.local_authentication_disabled`<br/>(via the AVM `local_authentication_disabled` input). Spec 005 / Q1c /<br/>research §6: MUST remain `false`. The Application Insights JavaScript SDK<br/>does NOT support Microsoft Entra ingestion authentication<br/>(https://learn.microsoft.com/azure/azure-monitor/app/azure-ad-authentication#unsupported-scenarios),<br/>so disabling local auth would break all browser telemetry. The backend<br/>.NET OpenTelemetry exporter authenticates via AAD using<br/>`APPLICATIONINSIGHTS_AUTHENTICATION_STRING = "Authorization=AAD;ClientId=..."`,<br/>which works ALONGSIDE local auth — not as a replacement for it. See<br/>README.md § Local authentication. | `bool` | `false` | no |
| <a name="input_retention_in_days"></a> [retention\_in\_days](#input\_retention\_in\_days) | Log Analytics workspace data-retention period in days. | `number` | `30` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to both monitoring resources. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_app_insights_connection_string_secret_uri"></a> [app\_insights\_connection\_string\_secret\_uri](#output\_app\_insights\_connection\_string\_secret\_uri) | Key Vault secret URI exposing the connection string (null when no Key Vault was passed). |
| <a name="output_application_insights_app_id"></a> [application\_insights\_app\_id](#output\_application\_insights\_app\_id) | Application Insights `app_id` — a stable GUID identifying the AI component in the REST query API. |
| <a name="output_application_insights_connection_string"></a> [application\_insights\_connection\_string](#output\_application\_insights\_connection\_string) | Application Insights connection string. Sensitive — prefer consuming via the Key Vault reference exposed by `app_insights_connection_string_secret_uri`. |
| <a name="output_application_insights_id"></a> [application\_insights\_id](#output\_application\_insights\_id) | Resource ID of the Application Insights component. |
| <a name="output_application_insights_name"></a> [application\_insights\_name](#output\_application\_insights\_name) | Application Insights resource name (echo of var.application\_insights\_name). |
| <a name="output_log_analytics_workspace_customer_id"></a> [log\_analytics\_workspace\_customer\_id](#output\_log\_analytics\_workspace\_customer\_id) | LAW customer (workspace) GUID — needed by some agents that authenticate against the workspace by GUID. |
| <a name="output_log_analytics_workspace_id"></a> [log\_analytics\_workspace\_id](#output\_log\_analytics\_workspace\_id) | Resource ID of the Log Analytics Workspace. Pass to other modules for diagnostic-settings sinks. |
<!-- END_TF_DOCS -->

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
