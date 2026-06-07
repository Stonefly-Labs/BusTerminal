# Spec 006 / T014 / research §5. Provisions the AI Search index defined by
# `specs/006-service-bus-registry-core/contracts/search-index.json`.
#
# Azure AI Search indexes are data-plane resources (no ARM endpoint exists
# at `management.azure.com/.../searchServices/{n}/indexes/{i}`); they live
# on the service's own REST endpoint at `{name}.search.windows.net`. The
# azapi provider's `azapi_data_plane_resource` targets that endpoint with
# the appropriate token audience — see
# https://learn.microsoft.com/azure/developer/terraform/concept-azapi-data-plane-framework
#
# The contract file is the single source of truth: the indexer's
# SearchDocumentMapper, the API's SearchClient queries, and this module all
# reference the same field set. Drift would surface in T060/T061 (web-side
# contract test + backend FluentValidation parity test).

locals {
  index_definition_raw = jsondecode(file(var.index_definition_path))
  index_name           = local.index_definition_raw.name

  # Strip the leading `$comment` (a JSON-Schema-style hint not consumed by
  # the Azure REST API) before submitting.
  index_body = {
    for k, v in local.index_definition_raw : k => v if k != "$comment"
  }
}

resource "azapi_data_plane_resource" "registry_index" {
  type      = "Microsoft.Search/searchServices/indexes@2024-07-01"
  parent_id = "${var.search_service_name}.search.windows.net"
  name      = local.index_name

  body = local.index_body

  # The index is recoverable from the contract file (no operator data lives
  # on the index — it's a projection of the canonical Cosmos store) so
  # prevent_destroy is intentionally OFF; replace is acceptable. The
  # `registry-entities-v1` name is the version pinning per indexer-events.md §7.
}
