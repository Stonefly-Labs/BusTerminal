# Spec 006 / T014 / research §5. Provisions the AI Search index defined by
# `specs/006-service-bus-registry-core/contracts/search-index.json` via the
# Azure REST envelope (azapi_resource) — neither azurerm v4 nor the AVM
# search module expose an index resource.
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

resource "azapi_resource" "registry_index" {
  type      = "Microsoft.Search/searchServices/indexes@2024-07-01"
  parent_id = var.ai_search_id
  name      = local.index_name

  body = local.index_body

  # The Microsoft.Search/searchServices/indexes resource type is not bundled
  # in the azapi v2.x provider schema (data-plane REST resources lag the
  # ARM resource catalog). The provider exposes a per-resource opt-out
  # that submits the body to the REST endpoint without local schema
  # validation. The contract file we read is the canonical schema source
  # of truth so the deferred validation lives one layer up.
  schema_validation_enabled = false

  # The index is recoverable from the contract file (no operator data lives
  # on the index — it's a projection of the canonical Cosmos store) so
  # prevent_destroy is intentionally OFF; replace is acceptable. The
  # `registry-entities-v1` name is the version pinning per indexer-events.md §7.
}
