# Cosmos registry store module

Spec 006 / T013. Provisions the SQL containers that back the Service Bus
registry slice on the existing `canonical` database (spec 004) of the
existing Cosmos account (spec 005). Spec 008 added the
`namespace-validation-runs` container; spec 009 adds the `discovery-runs`
and `discovery-locks` containers:

- `registry-entities` — partition key `/environment`. ETag-based optimistic
  concurrency, tombstone-then-delete pattern (research §10). Composite index
  `(parentId, entityType, name)` for duplicate-name + child-enumeration
  queries. `/metadata/*` excluded from indexing. Per-item TTL enabled
  (`default_ttl = -1`) so tombstone markers self-expire after 60s.
- `registry-audit` — partition key `/entityId`. Append-only from the user
  perspective (FR-034); no TTL in v1. Minimal indexing (only `/timestamp`,
  `/eventType`) — entity-scoped queries hit the partition key for free.
- `registry-entities-leases` — partition key `/id`. Cosmos change-feed
  lease state for the indexer (research §17 — managed-identity auth forbids
  the trigger from auto-creating the lease container).
- `namespace-validation-runs` (spec 008) — partition key `/namespaceId`.
  Append-only validation history; indefinite retention in v1.
- `discovery-runs` (spec 009) — partition key `/namespaceId`. Append-only
  history of discovery runs. Composite index `(/namespaceId,
  /startedUtc DESC)` powers the reverse-chronological history list.
  Indefinite retention in v1; carries `prevent_destroy = true`.
- `discovery-locks` (spec 009) — partition key `/namespaceId`. One document
  per registered namespace (id = `"lock"`); used for FR-003 coalescing via
  Cosmos ETag-based atomic acquisition (see data-model.md §1.3). No
  `prevent_destroy` — operators may safely replace it during recovery
  via `tools/DiscoveryLockReset`.

All long-lived data containers carry `lifecycle.prevent_destroy = true` per
BT-IAC-007. The account is serverless, so no throughput configuration is
set on the containers (the account capability rejects it).

See [`specs/006-service-bus-registry-core/data-model.md` §4.1](../../../specs/006-service-bus-registry-core/data-model.md#41-cosmos-containers)
and [`specs/009-entity-discovery-publication/data-model.md` §1.2-§1.3](../../../specs/009-entity-discovery-publication/data-model.md).

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_cosmosdb_sql_container.discovery_locks](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_container.discovery_runs](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_container.namespace_validation_runs](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_container.registry_audit](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_container.registry_entities](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_container.registry_entities_leases](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_audit_container_name"></a> [audit\_container\_name](#input\_audit\_container\_name) | Name of the registry-audit container. Partition key /entityId. Append-only writes (FR-034). | `string` | `"registry-audit"` | no |
| <a name="input_cosmos_account_name"></a> [cosmos\_account\_name](#input\_cosmos\_account\_name) | Name of the existing Cosmos DB account (output by the cosmos-account module). Required because `azurerm_cosmosdb_sql_container` references the account by name. | `string` | n/a | yes |
| <a name="input_cosmos_canonical_database_name"></a> [cosmos\_canonical\_database\_name](#input\_cosmos\_canonical\_database\_name) | Name of the existing 'canonical' database (output by cosmos-canonical-store). | `string` | `"canonical"` | no |
| <a name="input_discovery_locks_container_name"></a> [discovery\_locks\_container\_name](#input\_discovery\_locks\_container\_name) | Name of the discovery-locks container (spec 009). Partition key /namespaceId. One document per registered namespace (id='lock'); used for FR-003 coalescing via Cosmos ETag-based atomic acquisition. | `string` | `"discovery-locks"` | no |
| <a name="input_discovery_runs_container_name"></a> [discovery\_runs\_container\_name](#input\_discovery\_runs\_container\_name) | Name of the discovery-runs container (spec 009). Partition key /namespaceId. Append-only history of discovery runs; indefinite retention in v1. | `string` | `"discovery-runs"` | no |
| <a name="input_entities_container_name"></a> [entities\_container\_name](#input\_entities\_container\_name) | Name of the registry-entities container. Partition key /environment. | `string` | `"registry-entities"` | no |
| <a name="input_entity_default_ttl_seconds"></a> [entity\_default\_ttl\_seconds](#input\_entity\_default\_ttl\_seconds) | Default TTL on the registry-entities container. `-1` enables per-item TTL so the tombstone-then-delete pattern (research §10) can self-expire markers. Set to `0` (off) to disable per-item TTL — only do this in a future ops spec that replaces the tombstone approach. | `number` | `-1` | no |
| <a name="input_leases_container_name"></a> [leases\_container\_name](#input\_leases\_container\_name) | Name of the Cosmos change-feed lease container for the indexer (research §17). Partition key /id, required by the change-feed trigger. | `string` | `"registry-entities-leases"` | no |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Cosmos DB account. | `string` | n/a | yes |
| <a name="input_validation_runs_container_name"></a> [validation\_runs\_container\_name](#input\_validation\_runs\_container\_name) | Name of the namespace-validation-runs container (spec 008). Partition key /namespaceId. Append-only writes; indefinite retention in v1. | `string` | `"namespace-validation-runs"` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_audit_container_id"></a> [audit\_container\_id](#output\_audit\_container\_id) | ARM resource id of the registry-audit container. |
| <a name="output_audit_container_name"></a> [audit\_container\_name](#output\_audit\_container\_name) | Name of the registry-audit container. Consumed by CosmosRegistryOptions.AuditContainer. |
| <a name="output_discovery_locks_container_id"></a> [discovery\_locks\_container\_id](#output\_discovery\_locks\_container\_id) | ARM resource id of the discovery-locks container. |
| <a name="output_discovery_locks_container_name"></a> [discovery\_locks\_container\_name](#output\_discovery\_locks\_container\_name) | Name of the discovery-locks container. Consumed by CosmosRegistryOptions.DiscoveryLocksContainer (spec 009). |
| <a name="output_discovery_runs_container_id"></a> [discovery\_runs\_container\_id](#output\_discovery\_runs\_container\_id) | ARM resource id of the discovery-runs container. |
| <a name="output_discovery_runs_container_name"></a> [discovery\_runs\_container\_name](#output\_discovery\_runs\_container\_name) | Name of the discovery-runs container. Consumed by CosmosRegistryOptions.DiscoveryRunsContainer (spec 009). |
| <a name="output_entities_container_id"></a> [entities\_container\_id](#output\_entities\_container\_id) | ARM resource id of the registry-entities container. |
| <a name="output_entities_container_name"></a> [entities\_container\_name](#output\_entities\_container\_name) | Name of the registry-entities container. Consumed by CosmosRegistryOptions.EntitiesContainer. |
| <a name="output_leases_container_id"></a> [leases\_container\_id](#output\_leases\_container\_id) | ARM resource id of the leases container. |
| <a name="output_leases_container_name"></a> [leases\_container\_name](#output\_leases\_container\_name) | Name of the change-feed lease container. |
| <a name="output_validation_runs_container_id"></a> [validation\_runs\_container\_id](#output\_validation\_runs\_container\_id) | ARM resource id of the namespace-validation-runs container. |
| <a name="output_validation_runs_container_name"></a> [validation\_runs\_container\_name](#output\_validation\_runs\_container\_name) | Name of the namespace-validation-runs container. Consumed by CosmosRegistryOptions.ValidationRunsContainer (spec 008). |
<!-- END_TF_DOCS -->
