# Cosmos canonical store module

Provisions the SQL database + containers that back the canonical resource
store on the Cosmos DB account from the sibling `cosmos-account` module.

Two containers, one database (data-model.md entities 1 + 6):

- `resources` — partition key `/resourceType`. ETag-based optimistic
  concurrency (FR-025). Soft-delete via the `isDeleted` predicate filtered
  at query time (FR-020). The wholesale `/extensions/*` indexing exclusion
  supports FR-012's per-extension `__indexable` opt-back-in.
- `change-events` — partition key `/resourceId`. Append-only change log
  (FR-015 / Q5). One document per state change. Minimal indexing keeps
  RU cost low.

Uses raw `azurerm_cosmosdb_sql_*` resources rather than the AVM
`sql_databases` input because the cosmos-account module's AVM bypass
leaves database + container provisioning here. See
`iac/modules/cosmos-account/main.tf` for the AVM bypass rationale.

Spec 004 / Spec 005 / US7 — `lifecycle.prevent_destroy = true` on the
database (T121).

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_cosmosdb_sql_container.change_events](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_container.resources](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_container) | resource |
| [azurerm_cosmosdb_sql_database.canonical](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/cosmosdb_sql_database) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_cosmos_account_id"></a> [cosmos\_account\_id](#input\_cosmos\_account\_id) | ARM resource id of the Cosmos DB account (e.g.,<br/>`/subscriptions/.../databaseAccounts/<acct>`). Used ONLY to compute the<br/>`canonical_database_role_scope` output — the path-construction trap<br/>documented in research §15: `azurerm_cosmosdb_sql_role_assignment.scope`<br/>requires the data-plane path form (`<account-id>/dbs/<db-name>`), not the<br/>ARM form (`<account-id>/sqlDatabases/<db-name>`). Surface the correct<br/>shape from the module so consumers can't re-discover the trap. | `string` | n/a | yes |
| <a name="input_cosmos_account_name"></a> [cosmos\_account\_name](#input\_cosmos\_account\_name) | Name of the Cosmos DB account (output by the cosmos-account module). Required because `azurerm_cosmosdb_sql_database` references the account by name, not by id. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Cosmos DB account. Required by `azurerm_cosmosdb_sql_database`. | `string` | n/a | yes |
| <a name="input_change_events_container_name"></a> [change\_events\_container\_name](#input\_change\_events\_container\_name) | Name of the append-only change-event log container (FR-015 / Q5). | `string` | `"change-events"` | no |
| <a name="input_database_name"></a> [database\_name](#input\_database\_name) | Logical database name for the canonical store. | `string` | `"busterminal-canonical"` | no |
| <a name="input_resources_container_name"></a> [resources\_container\_name](#input\_resources\_container\_name) | Name of the container holding canonical resource documents + the relationship peer documents (per data-model.md §3, relationships live in the same container). | `string` | `"resources"` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_canonical_database_role_scope"></a> [canonical\_database\_role\_scope](#output\_canonical\_database\_role\_scope) | The exact scope path for `azurerm_cosmosdb_sql_role_assignment` consumers<br/>granting access to the canonical database. Uses the Cosmos data-plane<br/>path form (`<account-id>/dbs/<db-name>`), NOT the ARM form<br/>(`<account-id>/sqlDatabases/<db-name>`) — submitting the ARM form returns<br/>HTTP 400 "Expected path segment [dbs] at position [0] but found<br/>[sqlDatabases]" from the Cosmos provider. Surfacing the correct shape<br/>here prevents future specs from re-discovering this trap. See<br/>research.md §15. |
| <a name="output_change_events_container_name"></a> [change\_events\_container\_name](#output\_change\_events\_container\_name) | Append-only change-event log container. Bound to CosmosOptions.Containers.ChangeEvents. |
| <a name="output_database_id"></a> [database\_id](#output\_database\_id) | ARM resource id of the SQL database. Useful for ARM-shaped references (e.g., management-plane role assignments). NOT the right shape for Cosmos data-plane RBAC scope on `azurerm_cosmosdb_sql_role_assignment.scope` — that resource expects the data-plane path form (`.../databaseAccounts/<acct>/dbs/<db>`), not the ARM form (`.../sqlDatabases/<db>`). Construct that scope at the call site from the account id + `database_name`. |
| <a name="output_database_name"></a> [database\_name](#output\_database\_name) | Logical database name. Bound to CosmosOptions.Database. |
| <a name="output_resources_container_name"></a> [resources\_container\_name](#output\_resources\_container\_name) | Container holding canonical resource + relationship documents. Bound to CosmosOptions.Containers.Resources. |
<!-- END_TF_DOCS -->
