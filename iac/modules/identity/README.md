# Identity module

Wraps the Azure Verified Module
[`Azure/avm-res-managedidentity-userassignedidentity/azurerm` v0.3.3](https://registry.terraform.io/modules/Azure/avm-res-managedidentity-userassignedidentity/azurerm/0.3.3)
to provision a User-Assigned Managed Identity with an optional set of
role assignments threaded via the `role_assignments` input.

Lighter-weight peer of `workload-identity` — this module is used when a
component needs a UAMI with arbitrary scoped role assignments, but
without the workload-identity module's opinionated naming validation,
federated-credential plumbing, or Entra app-role assignment surface.

Spec 002 / Spec 005 — general-purpose UAMI primitive (e.g., the
platform-bootstrap deployment-MI; the Container Apps Environment's
log-publisher identity in later slices).

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_role_assignment.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/role_assignment) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the identity. | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Name of the user-assigned managed identity. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group holding the identity. | `string` | n/a | yes |
| <a name="input_role_assignments"></a> [role\_assignments](#input\_role\_assignments) | Map of role assignments to create. Key is a stable identifier (e.g., `kv-secrets-user`); value contains the role name and scope. | <pre>map(object({<br/>    role_definition_name = string<br/>    scope                = string<br/>  }))</pre> | `{}` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the identity. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_client_id"></a> [client\_id](#output\_client\_id) | Client ID (applicationId) — set this on workload pods/containers as the federated/managed-identity client id. |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the managed identity. |
| <a name="output_name"></a> [name](#output\_name) | Identity name (echoed for downstream references). |
| <a name="output_principal_id"></a> [principal\_id](#output\_principal\_id) | Object ID (principal ID) for RBAC assignments. |
<!-- END_TF_DOCS -->
