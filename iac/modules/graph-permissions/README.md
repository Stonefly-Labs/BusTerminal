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
