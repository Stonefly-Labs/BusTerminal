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
