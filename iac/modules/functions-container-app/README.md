# Functions container app module

Spec 006 / T015. Provisions a Container App that hosts the registry
indexer Functions image on the existing Container Apps Environment from
spec 005. Per [research §4](../../../specs/006-service-bus-registry-core/research.md)
this is the **v2 native Functions-on-CAE** hosting model — single
`azurerm_container_app` resource, no proxy `Microsoft.Web/sites` Function
App, no separate hidden container.

The container exposes no public ingress; its only execution surface is the
Cosmos change-feed trigger declared by
`api/BusTerminal.Indexer/Functions/RegistryEntityIndexer.cs`. CAE health
probes use the Functions runtime's internal `/healthz` endpoint.

Inputs bind the workload UAMI (spec 005) and inject the Cosmos / AI Search
endpoint environment variables documented in
[`indexer-events.md`](../../../specs/006-service-bus-registry-core/contracts/indexer-events.md).

Diagnostic settings attach via `iac/modules/diagnostic-settings` in the env
composition; this module does not provision them directly.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_container_app.indexer](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/container_app) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_ai_search_endpoint"></a> [ai\_search\_endpoint](#input\_ai\_search\_endpoint) | AI Search service endpoint URI. | `string` | n/a | yes |
| <a name="input_ai_search_index_name"></a> [ai\_search\_index\_name](#input\_ai\_search\_index\_name) | AI Search index name (typically `registry-entities-v1`). | `string` | n/a | yes |
| <a name="input_app_insights_connection_string_kv_secret_uri"></a> [app\_insights\_connection\_string\_kv\_secret\_uri](#input\_app\_insights\_connection\_string\_kv\_secret\_uri) | Key Vault secret URI exposing the App Insights connection string. Mirrors the spec-005 hybrid AI ingestion pattern. | `string` | n/a | yes |
| <a name="input_container_apps_environment_id"></a> [container\_apps\_environment\_id](#input\_container\_apps\_environment\_id) | Container Apps Environment resource id (from spec 005). | `string` | n/a | yes |
| <a name="input_container_image"></a> [container\_image](#input\_container\_image) | Fully-qualified container image reference (registry/name:tag). | `string` | n/a | yes |
| <a name="input_cosmos_account_endpoint"></a> [cosmos\_account\_endpoint](#input\_cosmos\_account\_endpoint) | Cosmos DB account endpoint URI (e.g., https://<acct>.documents.azure.com:443/). | `string` | n/a | yes |
| <a name="input_cosmos_database_name"></a> [cosmos\_database\_name](#input\_cosmos\_database\_name) | Cosmos database name (typically `canonical`). | `string` | n/a | yes |
| <a name="input_cosmos_entities_container_name"></a> [cosmos\_entities\_container\_name](#input\_cosmos\_entities\_container\_name) | Cosmos container name holding registry entities. | `string` | n/a | yes |
| <a name="input_cosmos_leases_container_name"></a> [cosmos\_leases\_container\_name](#input\_cosmos\_leases\_container\_name) | Cosmos container name holding change-feed lease state. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Container App name. | `string` | n/a | yes |
| <a name="input_registry_login_server"></a> [registry\_login\_server](#input\_registry\_login\_server) | ACR login server (from spec 005). | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Container App. | `string` | n/a | yes |
| <a name="input_workload_uami_client_id"></a> [workload\_uami\_client\_id](#input\_workload\_uami\_client\_id) | Workload UAMI client id. Injected as `AZURE_CLIENT_ID` and `Cosmos__clientId`. | `string` | n/a | yes |
| <a name="input_workload_uami_id"></a> [workload\_uami\_id](#input\_workload\_uami\_id) | Workload user-assigned managed identity resource id (from spec 005). | `string` | n/a | yes |
| <a name="input_cpu"></a> [cpu](#input\_cpu) | vCPU per replica. | `number` | `0.5` | no |
| <a name="input_max_replicas"></a> [max\_replicas](#input\_max\_replicas) | Maximum replicas. Single replica is sufficient for the spec-006 scale (research §3 + §16). | `number` | `1` | no |
| <a name="input_memory"></a> [memory](#input\_memory) | Memory per replica (Gi). | `string` | `"1Gi"` | no |
| <a name="input_min_replicas"></a> [min\_replicas](#input\_min\_replicas) | Minimum replicas. The change-feed trigger keeps at least one warm so leases don't churn. | `number` | `1` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Resource tags. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_container_app_fqdn"></a> [container\_app\_fqdn](#output\_container\_app\_fqdn) | Internal FQDN on the CAE default domain. No public ingress is configured in this slice (the indexer has no inbound HTTP surface). |
| <a name="output_container_app_id"></a> [container\_app\_id](#output\_container\_app\_id) | Container App ARM resource id. Consumed by the env composition for diagnostic settings. |
| <a name="output_container_app_name"></a> [container\_app\_name](#output\_container\_app\_name) | Container App resource name. Useful for `az containerapp ...` follow-up commands. |
<!-- END_TF_DOCS -->
