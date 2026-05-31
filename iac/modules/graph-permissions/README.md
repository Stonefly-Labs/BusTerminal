# `iac/modules/graph-permissions`

Declares Microsoft Graph **application** permissions on a BusTerminal API app
registration. Introduced by spec 003 (US6, FR-024).

The module:

- Uses the modern `azuread_application_api_access` resource (not the legacy
  `required_resource_access` block) per `specs/003-auth-and-identity/research.md` § 6.
- Resolves Microsoft Graph's well-known app id via the
  `azuread_application_published_app_ids` data source — no hard-coded UUID.
- Declares **application** (app-only) permissions only. Delegated scopes are
  intentionally omitted; this slice's Graph flows are app-only (FR-025).

The module does **not**:

- Grant admin consent. Admin consent is a tenant-admin manual step performed
  once per environment via the Entra portal or
  `az ad app permission admin-consent`. The procedure lives in
  `specs/003-auth-and-identity/quickstart.md` § A.2.3 and (post-implementation)
  in `docs/identity-graph-permissions.md`.

## Inventory rule

Every permission UUID passed via `granted_application_permission_ids` MUST also
appear in `specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`
with a rationale. The inventory is reviewer-enforced — IaC changes without an
inventory update are rejected.

## Permission UUID reference

Application permission role UUIDs come from the
[Microsoft Graph permissions reference](https://learn.microsoft.com/graph/permissions-reference).
Current slice-003 entries:

| Permission | Type | UUID |
|---|---|---|
| `User.Read.All` | Application | `df021288-bdef-4463-88db-98f22de89214` |

## Example

```hcl
module "graph_permissions" {
  source = "../../modules/graph-permissions"

  api_application_id = data.azuread_application.api.id

  granted_application_permission_ids = [
    "df021288-bdef-4463-88db-98f22de89214", # User.Read.All
  ]
}
```

After `tofu apply`, a tenant admin must grant consent for the permission in the
target environment before any `IGraphClient` call will succeed at runtime —
calls that hit the SDK before consent emit a "Insufficient privileges to
complete the operation" error from Graph.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azuread_application_api_access.graph](https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/application_api_access) | resource |
| [azuread_application_published_app_ids.well_known](https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/data-sources/application_published_app_ids) | data source |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_api_application_id"></a> [api\_application\_id](#input\_api\_application\_id) | Resource ID of the API `azuread_application` the Graph permissions attach to.<br/>Pass the `id` attribute of either a managed `azuread_application` resource<br/>or a `data "azuread_application"` source for an app registration that<br/>lives outside tofu state. | `string` | n/a | yes |
| <a name="input_granted_application_permission_ids"></a> [granted\_application\_permission\_ids](#input\_granted\_application\_permission\_ids) | List of Microsoft Graph **application-permission** role UUIDs the API app<br/>registration is requesting. Every entry MUST also appear in<br/>`specs/003-auth-and-identity/contracts/graph-permissions-inventory.md`<br/>(or the durable successor doc) with a rationale — adding here without<br/>updating the inventory is a defect.<br/><br/>Current entries (slice 003):<br/>  - `df021288-bdef-4463-88db-98f22de89214` → `User.Read.All` (Application)<br/><br/>Reference: https://learn.microsoft.com/graph/permissions-reference | `list(string)` | n/a | yes |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_granted_role_ids"></a> [granted\_role\_ids](#output\_granted\_role\_ids) | The list of Microsoft Graph application-permission UUIDs that this module<br/>requested on the target app registration. Mirrors<br/>`var.granted_application_permission_ids` and is exposed so downstream<br/>documentation generators / drift detectors can compare against the<br/>inventory document without re-reading variables. |
| <a name="output_graph_api_client_id"></a> [graph\_api\_client\_id](#output\_graph\_api\_client\_id) | Microsoft Graph's well-known app id (resolved via the `azuread_application_published_app_ids` data source). Useful for downstream `azuread_app_role_assignment` resources if a future slice needs to assign individual Graph app roles directly. |
<!-- END_TF_DOCS -->

