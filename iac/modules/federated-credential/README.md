# federated-credential

Generalized federated identity credential (FIC) bound to a parent BusTerminal
managed identity. Spec 003, FR-029 / FR-030; data-model.md § Federated
Credential.

## What you get

- `azurerm_federated_identity_credential` (one resource) on the supplied
  parent UAMI, with caller-supplied `subject` and sensible defaults for
  `issuer` (GitHub Actions) and `audience` (Entra-mandated value).

## Why `azurerm_*` and not `azuread_application_*`

The Azure platform exposes two FIC resource types:

| Resource | Parent | When to use |
|---|---|---|
| `azurerm_federated_identity_credential` | user-assigned managed identity | BusTerminal default — every federation grant in this slice targets an MI (pipeline MI, workload MI). |
| `azuread_application_federated_identity_credential` | Entra ID app registration | Future use cases where the federation target is a service-principal-backed app reg rather than a MI. |

The data-model entity definition (`ParentIdentity` = reference to Workload
Identity) and every existing FIC in the codebase target an MI, so this module
produces the MI variant. If a future slice introduces an app-reg-parented FIC
we will add a sibling module rather than overload this one.

## Why the schema is narrower than the original spec text

`tasks.md` § T075 lists `display_name` and `description` as module inputs.
The underlying `azurerm_federated_identity_credential` resource exposes
neither (`name` is the display name; there is no description field). The
module follows the resource. Operators document a credential's intent via
the surrounding HCL comment + the entry the data model requires in
`docs/identity-and-secrets.md`.

## Usage

```hcl
# Pipeline MI federation, per-environment subject:
module "pipeline_federation_dev" {
  source = "../modules/federated-credential"

  name                = "github-environment-dev"
  resource_group_name = azurerm_resource_group.tfstate.name
  parent_id           = module.pipeline_identity["dev"].resource_id
  subject             = "repo:${var.github_org_repo}:environment:dev"
  # issuer/audience default to GitHub Actions + Entra workload-identity values.
}

# Workload MI federation, env-scoped:
module "workload_federation_environment" {
  source = "../../modules/federated-credential"

  name                = "github-environment-${var.environment_name}-workload"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = module.workload_identity.id
  subject             = "repo:${var.github_org_repo}:environment:${var.environment_name}"
}
```

## Validation rules (encoded in `variables.tf`)

- `name` 1–120 characters
- `parent_id` must be a fully-qualified UAMI resource ID
- `issuer` must be HTTPS (defaults to GitHub Actions)
- `audience` must be non-empty (defaults to `api://AzureADTokenExchange`)
- `subject` must be non-empty and **must not contain `*`** — wildcards
  widen trust beyond a single repo/branch/env and require an ADR-recorded
  exception per data-model.md § Federated Credential

<!-- BEGIN_TF_DOCS -->
## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_federated_identity_credential.this](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/federated_identity_credential) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_name"></a> [name](#input\_name) | Display name shown in the Entra portal and surfaced in federation-failure<br/>diagnostics (FR-030). Convention: `github-environment-<env>` for<br/>environment-scoped subjects; `github-branch-<branch>` for branch-scoped<br/>subjects. | `string` | n/a | yes |
| <a name="input_parent_id"></a> [parent\_id](#input\_parent\_id) | Resource ID of the parent user-assigned managed identity that this<br/>credential federates *to*. Typically `module.workload_identity.id` or<br/>`module.pipeline_identity[<env>].resource_id`. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group of the parent managed identity. Required by the underlying provider even though the FIC itself is a child resource of the MI. | `string` | n/a | yes |
| <a name="input_subject"></a> [subject](#input\_subject) | Federation subject pattern that the inbound OIDC token's `sub` claim<br/>must match exactly. Common shapes:<br/>  - `repo:<org>/<repo>:environment:<env>`  (deployment env subjects)<br/>  - `repo:<org>/<repo>:ref:refs/heads/<branch>`  (branch subjects)<br/>  - `repo:<org>/<repo>:pull_request`  (PR subjects — avoid for prod)<br/>Per data-model.md § Federated Credential, the chosen subject MUST also<br/>appear verbatim in `docs/identity-and-secrets.md` so federation-drift<br/>failures can be diagnosed in one step. | `string` | n/a | yes |
| <a name="input_audience"></a> [audience](#input\_audience) | Federation audience value. Entra mandates `api://AzureADTokenExchange`<br/>for workload-identity federation and rejects all other values, so the<br/>default fits every current use case. Surfaced as a variable for<br/>forward-compatibility only. | `string` | `"api://AzureADTokenExchange"` | no |
| <a name="input_issuer"></a> [issuer](#input\_issuer) | OIDC issuer URL of the external identity provider. Defaults to<br/>GitHub Actions' issuer; override for other CI systems or external<br/>workloads. Must be HTTPS. | `string` | `"https://token.actions.githubusercontent.com"` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_credential_id"></a> [credential\_id](#output\_credential\_id) | Resource ID of the federated identity credential. Useful for `depends_on` wiring in downstream resources that race FIC propagation. |
| <a name="output_name"></a> [name](#output\_name) | Echoes the credential's display name for downstream references and documentation. |
| <a name="output_subject"></a> [subject](#output\_subject) | Echoes the federation subject pattern. Surface this in plan output so PR reviewers can sanity-check the subject without reading the module call. |
<!-- END_TF_DOCS -->

