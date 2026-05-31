# app-registration-roles

Declares the four BusTerminal platform app roles (Admin / Operator / Reader /
Developer) on a target Microsoft Entra ID API app registration. One
`azuread_application_app_role` resource per role definition.

Spec ref: `specs/003-auth-and-identity/contracts/role-permission-matrix.md`,
`specs/003-auth-and-identity/tasks.md` T029–T031.

## When the parent app registration is outside tofu state

This is the dev environment's situation (the `bt-dev-api` app registration was
created out-of-band in spec 002, and only its client ID is supplied via
`TF_VAR_entra_api_client_id`). Reference the app via a `data` source:

```hcl
data "azuread_application" "api" {
  client_id = var.entra_api_client_id
}

module "app_registration_roles" {
  source = "../../modules/app-registration-roles"

  api_application_id = data.azuread_application.api.id

  role_definitions = {
    admin     = { ... }
    operator  = { ... }
    reader    = { ... }
    developer = { ... }
  }
}
```

No `lifecycle { ignore_changes = [app_role] }` is needed because the parent
is not a managed resource.

## When the parent app registration is managed by tofu

The parent `azuread_application` block must carry the lifecycle override:

```hcl
resource "azuread_application" "api" {
  display_name = "bt-${var.environment_name}-api"
  # ... other fields ...

  lifecycle {
    ignore_changes = [app_role]
  }
}

module "app_registration_roles" {
  source             = "../../modules/app-registration-roles"
  api_application_id = azuread_application.api.id
  role_definitions   = { ... }
}
```

Without the lifecycle override the inline `app_role` blocks on the parent will
fight the `azuread_application_app_role` resources every plan.

## Stable role IDs

The `role_id` (UUID) of each role MUST stay constant across applies — Entra
treats `role_id` as the identity of the role. Generate once (e.g. via
`uuidgen`), commit the value, never let `random_uuid` regenerate.

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azuread_application_app_role.this](https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/application_app_role) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_api_application_id"></a> [api\_application\_id](#input\_api\_application\_id) | Resource ID of the API `azuread_application` the roles attach to. Pass the<br/>`id` attribute of either a managed `azuread_application` resource (in which<br/>case its block must declare `lifecycle { ignore_changes = [app_role] }`) or<br/>a `data "azuread_application"` source for an app registration that lives<br/>outside tofu state. | `string` | n/a | yes |
| <a name="input_role_definitions"></a> [role\_definitions](#input\_role\_definitions) | Map of platform role definitions to declare on the target app registration.<br/>Each key is a stable role nickname (e.g. `admin`, `operator`, `reader`,<br/>`developer`); each value carries the on-wire role claim value, display<br/>metadata, and the stable role GUID used as the resource's role\_id. | <pre>map(object({<br/>    role_id              = string<br/>    value                = string<br/>    display_name         = string<br/>    description          = string<br/>    allowed_member_types = list(string)<br/>  }))</pre> | n/a | yes |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_role_ids"></a> [role\_ids](#output\_role\_ids) | Map of role nickname → role\_id (UUID) for downstream `azuread_app_role_assignment` consumers. |
| <a name="output_role_values"></a> [role\_values](#output\_role\_values) | Map of role nickname → on-wire role claim value (e.g. `BusTerminal.Admin`). |
<!-- END_TF_DOCS -->

