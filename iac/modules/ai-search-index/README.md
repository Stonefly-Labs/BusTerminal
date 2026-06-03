# AI Search index module

Spec 006 / Phase 1 T009 — module skeleton. Phase 2 T014 fills the body.

Provisions the `registry-entities-v1` search index on the existing AI Search
service from spec 005. Index field set is the single source of truth in
[`specs/006-service-bus-registry-core/contracts/search-index.json`](../../../specs/006-service-bus-registry-core/contracts/search-index.json)
and is read by this module via `jsondecode(file(...))`.

Implementation uses the `azapi` provider (`azapi_resource` with type
`Microsoft.Search/searchServices/indexes@2024-07-01`) because `azurerm` v4
does not expose index resources and the AVM for AI Search only covers the
service. See [`research.md` §5](../../../specs/006-service-bus-registry-core/research.md) for rationale.

<!-- BEGIN_TF_DOCS -->
<!-- END_TF_DOCS -->
