# AI Search index module

Spec 006 / T014. Provisions the `registry-entities-v1` search index on the
existing AI Search service from spec 005. Index field set is the single
source of truth in
[`specs/006-service-bus-registry-core/contracts/search-index.json`](../../../specs/006-service-bus-registry-core/contracts/search-index.json)
and is read by this module via `jsondecode(file(...))`.

Implementation uses the `azapi` provider (`azapi_resource` with type
`Microsoft.Search/searchServices/indexes@2024-07-01`) because `azurerm` v4
does not expose index resources and the AVM for AI Search only covers the
service. The provider's bundled schema does not yet include data-plane
search resources, so `schema_validation_enabled = false` is set on the
resource (the canonical schema lives in the contract file).
See [`research.md` §5](../../../specs/006-service-bus-registry-core/research.md) for rationale.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azapi_resource.registry_index](https://registry.terraform.io/providers/Azure/azapi/latest/docs/resources/resource) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_ai_search_id"></a> [ai\_search\_id](#input\_ai\_search\_id) | Resource id of the AI Search service from spec 005. | `string` | n/a | yes |
| <a name="input_index_definition_path"></a> [index\_definition\_path](#input\_index\_definition\_path) | Path to the JSON file containing the search index definition. Defaults to the spec-006 contract. | `string` | `"../../../specs/006-service-bus-registry-core/contracts/search-index.json"` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_index_id"></a> [index\_id](#output\_index\_id) | Full azapi-managed resource id of the index. |
| <a name="output_index_name"></a> [index\_name](#output\_index\_name) | Name of the registry search index. Bound to AiSearchOptions.IndexName. |
<!-- END_TF_DOCS -->
