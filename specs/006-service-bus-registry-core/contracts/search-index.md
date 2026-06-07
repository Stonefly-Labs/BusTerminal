# `registry-entities-v1` search index

Companion documentation for [`search-index.json`](./search-index.json).

This contract is the source of truth for the Azure AI Search index
`registry-entities-v1`:

- Read by [`iac/modules/ai-search-index`](../../../iac/modules/ai-search-index/) via `jsondecode(file(...))` and applied through `azapi_data_plane_resource`.
- Read by [`api/BusTerminal.Indexer/Indexing/SearchDocumentMapper.cs`](../../../api/BusTerminal.Indexer/Indexing/SearchDocumentMapper.cs) (by field name) to ensure the projection shape matches.
- Driven by [`data-model.md §6`](../data-model.md).

Author annotations live here rather than as `$comment` properties on the
JSON itself — Azure AI Search's data-plane API rejects unrecognized
property names (`Microsoft.Azure.Search.V2024_07_01.SchemaField` has no
`$comment`).

## Field-level notes

| Field | Notes |
| --- | --- |
| `tagKeysLower` | Lowercase-projected key set; supports case-insensitive key-only filter (FR-023 + research §9). |
| `metadataFlat` | Flattened `key=value` strings from `metadata`; discoverable via FR-022 full-text search. |
| `brokerKind` | Reserved for future multi-broker support (Principle VI). Always `'AzureServiceBus'` in this slice. |
