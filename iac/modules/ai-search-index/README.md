# AI Search index module

Spec 006 / T014. Provisions the `registry-entities-v1` search index on the
existing AI Search service from spec 005. Index field set is the single
source of truth in
[`specs/006-service-bus-registry-core/contracts/search-index.json`](../../../specs/006-service-bus-registry-core/contracts/search-index.json)
and is read by this module via `jsondecode(file(...))`.

Spec 009 / T006 extends the schema additively with `lifecycleStatus`,
`associatedServiceIds`, `associationRoles`, `firstDiscoveredUtc`,
`lastSeenUtc`, and the `azureSourced` complex type — all backward-compatible
(legacy documents missing these fields are simply absent from the
corresponding filters). The schema's `name` (and thus the index version
suffix `-v1`) is unchanged: AI Search accepts non-breaking field additions
in-place. Historical documents are backfilled by an off-cycle
canonical-rebuild run (see spec 009 `tasks.md` T114).

Implementation uses the `azapi` provider's `azapi_data_plane_resource`
(type `Microsoft.Search/searchServices/indexes@2024-07-01`) because Azure
AI Search indexes are data-plane resources living on the service endpoint
at `{name}.search.windows.net`, not ARM-tracked under
`management.azure.com`. The azapi provider's data-plane framework knows
the correct hostname pattern and token audience for this resource type —
see [Understand the AzAPI data plane
framework](https://learn.microsoft.com/azure/developer/terraform/concept-azapi-data-plane-framework).
See [`research.md` §5](../../../specs/006-service-bus-registry-core/research.md) for rationale.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azapi_data_plane_resource.registry_index](https://registry.terraform.io/providers/Azure/azapi/latest/docs/resources/data_plane_resource) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_search_service_name"></a> [search\_service\_name](#input\_search\_service\_name) | Name of the AI Search service from spec 005. Used to construct the data-plane hostname (`{name}.search.windows.net`) that azapi\_data\_plane\_resource targets. | `string` | n/a | yes |
| <a name="input_index_definition_path"></a> [index\_definition\_path](#input\_index\_definition\_path) | Path to the JSON file containing the search index definition. Defaults to the spec-006 contract. | `string` | `"../../../specs/006-service-bus-registry-core/contracts/search-index.json"` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_index_id"></a> [index\_id](#output\_index\_id) | azapi-managed data-plane resource id of the index. |
| <a name="output_index_name"></a> [index\_name](#output\_index\_name) | Name of the registry search index. Bound to AiSearchOptions.IndexName. |
<!-- END_TF_DOCS -->
