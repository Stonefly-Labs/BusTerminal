# Deploying BusTerminal environments

This guide describes how BusTerminal environments are deployed end-to-end, how
the CD pipeline is wired, and how to roll back if a deploy goes wrong. The
foundation slice ships a working `dev` environment; `test` and `prod` are
scaffolded as patterns and provisioned by following the same procedure.

> **TL;DR** — once the platform is bootstrapped and Entra app registrations
> exist, a single commit to `main` builds both container images, applies
> `iac/environments/dev/`, rolls the Container App revisions, and runs a
> post-deploy smoke check. Target wall-clock for a no-infra-change deploy is
> under 20 minutes (success criterion SC-002).

---

## 1. One-time platform bootstrap

The `dev` environment cannot be provisioned until the BusTerminal platform
bootstrap has run **once per Azure subscription**. The bootstrap creates:

- A resource group + storage account holding the OpenTofu remote state
- A `tfstate` blob container
- Per-environment user-assigned managed identities with federated identity
  credentials bound to `repo:<org>/BusTerminal:environment:<env>`
- Subscription-scoped `Contributor` and (narrowly conditioned) `Role Based
  Access Control Administrator` role assignments on each pipeline identity
- A `Storage Blob Data Contributor` role assignment on the state account

Two paths are supported and produce identical end states:

| Path | When to use | Reference |
|------|-------------|-----------|
| OpenTofu module | You have OpenTofu installed and `Owner` rights on the subscription. | [`iac/platform-bootstrap/README.md`](../iac/platform-bootstrap/README.md) and the wrapper script [`scripts/bootstrap-platform.{sh,ps1}`](../scripts/) |
| Manual `az` CLI | OpenTofu cannot run locally (subscription guardrails, etc.). | [`scripts/bootstrap-platform-manual.md`](../scripts/bootstrap-platform-manual.md) |

Both paths emit the same set of values that must be configured in GitHub:

### Repository-scoped variables

| Variable | Source |
|----------|--------|
| `AZURE_TENANT_ID` | bootstrap output |
| `AZURE_SUBSCRIPTION_ID` | bootstrap input |
| `TFSTATE_RESOURCE_GROUP` | bootstrap output |
| `TFSTATE_STORAGE_ACCOUNT_NAME` | bootstrap input |
| `TFSTATE_CONTAINER_NAME` | bootstrap output (`tfstate`) |

### Environment-scoped variables (`dev`, `test`, `prod`)

| Variable | Source |
|----------|--------|
| `AZURE_CLIENT_ID` | client ID of `mi-busterminal-pipeline-<env>` |
| `UNIQUE_SUFFIX_<ENV>` | 4–12-char alphanumeric suffix used in globally-unique resource names (Key Vault, ACR) |
| `ENTRA_TENANT_ID_<ENV>` | Microsoft Entra ID tenant ID enforced for user sign-in |
| `ENTRA_API_CLIENT_ID_<ENV>` | application (client) ID of the backend API registration |
| `ENTRA_WEB_CLIENT_ID_<ENV>` | application (client) ID of the frontend (web) registration |

> The `_<ENV>` suffix on the per-environment variables is convention only;
> GitHub environment variables are scoped by the environment they live in, so
> `AZURE_CLIENT_ID` in the `dev` environment is automatically distinct from
> `AZURE_CLIENT_ID` in the `test` environment.

---

## 2. Entra app registration one-time setup

Two Entra ID applications must exist per environment before the first deploy:

- **Backend API registration** (audience for the JWT the backend validates)
  - Application ID URI: `api://<api-client-id>`
  - Exposed scope: `access_as_user`
  - Platform app roles: `BusTerminal.{Admin,Operator,Reader,Developer}` declared via IaC (`module.app_registration_roles`)
- **Frontend (web) SPA registration** (MSAL Authorization Code + PKCE sign-in lands here)
  - Single-page application (SPA) platform with redirect URI `https://<frontend-fqdn>` and `http://localhost:3000` for local dev
  - **No client secret** — SPAs are public clients; PKCE replaces the secret
  - API permission: `access_as_user` on the backend API registration, admin consented

See [`identity-and-secrets.md`](./identity-and-secrets.md) for the
authoritative explanation of every credential mechanism BusTerminal uses.
For deploy, the only requirement is that the resulting client IDs are
configured as the `entra_*_client_id` IaC variables above.

---

## 3. Pipeline trigger model

| Workflow | Trigger | Identity used | Effect |
|----------|---------|---------------|--------|
| [`ci.yml`](../.github/workflows/ci.yml) | Every PR + push to `main` | `GITHUB_TOKEN` only | Lints, builds, tests, scans. No Azure access. |
| [`iac-validate.yml`](../.github/workflows/iac-validate.yml) | PRs touching `iac/**` | `dev` environment identity, OIDC | `tofu fmt/validate/tflint/checkov` + `tofu plan` posted as a PR comment. No apply. |
| [`cd-dev.yml`](../.github/workflows/cd-dev.yml) | Push to `main` | `dev` environment identity, OIDC | Build + push images, `tofu apply` dev, roll revisions, health-wait, smoke. |

The federated credential subject pattern (`repo:<org>/BusTerminal:environment:<env>`)
binds every CD job to a single GitHub deployment environment. PRs cannot
deploy: pull-request branches do not match any federated subject and therefore
cannot exchange an OIDC token for an Azure access token. This is the
environment-isolation guarantee FR-100 requires.

---

## 4. Anatomy of a `dev` deploy

A push to `main` runs `cd-dev.yml` with the following ordered steps:

1. **Checkout** + read `iac/.terraform-version`.
2. **Setup OpenTofu** at the pinned version.
3. **Azure login (OIDC)** — exchanges the GitHub OIDC token for an Azure
   access token via the `dev` environment's federated managed identity.
4. **Resolve naming** — computes the ACR name (`acrbtdev<unique-suffix>`) and
   the per-deploy image tags (`<github-sha>`).
5. **`tofu init`** — partial backend config supplied via `-backend-config=`
   flags pointing at the bootstrap state account; `use_oidc=true` and
   `use_azuread_auth=true` are pinned in [`backend.tf`](../iac/environments/dev/backend.tf).
6. **Infra-only `tofu apply`** — first apply uses the prior deploy's image
   refs (read from `tofu output`) or a public placeholder on first-ever runs.
   Ensures ACR exists before the build step can push to it.
7. **`az acr login`** — managed-identity-backed ACR auth.
8. **Build & push backend image** — `api/BusTerminal.Api/Dockerfile` →
   `<acr>.azurecr.io/busterminal/api:<sha>` and `:latest`.
9. **Build & push frontend image** — `web/Dockerfile` →
   `<acr>.azurecr.io/busterminal/web:<sha>` and `:latest`.
10. **Roll-revisions `tofu apply`** — re-applies with the freshly-pushed image
    tags. The Container App resources see only their `image` argument change,
    so the platform creates new revisions and shifts 100% of traffic once the
    new revisions report healthy.
11. **Health wait** — polls `${backend_fqdn}/healthz/ready` and the frontend
    root for up to 5 minutes each. A non-200 final response fails the deploy.
12. **Unauthenticated smoke** — confirms `${backend_fqdn}/whoami` returns 401
    with a `WWW-Authenticate: Bearer ...` challenge (proves auth is enforced
    at the network edge before any application logic).
13. **Authenticated smoke** — acquires a token via `az account get-access-token`
    against `api://${ENTRA_API_CLIENT_ID_DEV}` and calls `/whoami` with a
    generated `traceparent`. Validates the response carries `principal.oid`,
    `correlation.traceId`, and `server.environment` per the OpenAPI contract.
    See § 5 for the one-time grant this step requires.

---

## 5. Granting the pipeline identity access to the backend API

The authenticated smoke step (T072) requires the `dev` pipeline managed
identity to be authorized as a client of the backend API so it can mint an
app-only API token (`az account get-access-token --resource api://<api>`) and
call `GET /whoami`.

**This is now managed by IaC — no manual steps.** The `dev` composition
declares a dedicated app-only role `Smoke.Invoke` on the backend API
registration and assigns it to the pipeline managed identity:

- `azuread_application_app_role.pipeline_smoke` — the app-only role
  (`allowed_member_types = ["Application"]`, value `Smoke.Invoke`).
- `azuread_app_role_assignment.pipeline_smoke` — grants it to
  `mi-busterminal-pipeline-dev` (resolved from
  `TF_VAR_pipeline_identity_client_id`, wired from the `AZURE_CLIENT_ID`
  deployment variable in `cd-dev.yml` / `iac-apply-dev.yml`).

Both resources are created by the pipeline's own `tofu apply` — the pipeline
MI **owns** the `bt-dev-api` app registration, so no tenant-wide Graph
permission or admin consent is required. (Contrast with the Microsoft Graph
*application permissions* in §A.2, which DO require manual admin consent.)

`Smoke.Invoke` deliberately confers **no platform authorization** — the
backend's role parser does not map it to any operation class, so `/whoami`
returns 200 with an empty `effectiveRoles`. The pipeline identity is not
granted any `BusTerminal.*` role.

The authenticated smoke step is `continue-on-error` so the first deploy after
this lands (and any window where the role assignment has not yet propagated,
~1–2 min) reports a warning rather than failing. Once the assignment is live
and confirmed green, it can be promoted to a hard gate by removing
`continue-on-error` on the `smoke_auth` step in `cd-dev.yml`.

---

## 6. Rollback

Azure Container Apps preserves the previous revision when a new revision is
created. If a deploy is broken:

1. **Revert the offending PR on `main`** — `git revert <sha> && git push`.
   The CD pipeline runs again, builds images from the reverted source, and
   rolls revisions back to the prior state.
2. **Manual revision shift (faster, if revert is not yet safe)** — in the
   Azure portal or via CLI, shift 100% of traffic back to the previous
   revision while the regression is investigated:

```sh
az containerapp revision list \
  --name ca-bt-dev-api \
  --resource-group rg-bt-dev \
  --query "[].{name:name, active:properties.active, trafficWeight:properties.trafficWeight}"

az containerapp ingress traffic set \
  --name ca-bt-dev-api \
  --resource-group rg-bt-dev \
  --revision-weight <previous-revision-name>=100 <current-revision-name>=0
```

The new revision is left in `Active` state but receives no traffic, allowing
later root-cause investigation via its logs in Application Insights.

---

## 7. Adding a new environment

The summary (the per-env scaffold itself lands when the first non-dev
environment is provisioned):

1. Re-run the bootstrap with the additional environment name (`-e test`) so
   the matching managed identity + federated credential is created. The
   bootstrap uses [`modules/federated-credential`](../iac/modules/federated-credential/)
   for the pipeline FIC, so any new env automatically inherits the right
   subject shape.
2. Copy `iac/environments/dev/*` to `iac/environments/test/`, renaming the
   backend key (`envs/test/terraform.tfstate`).
3. Configure environment-scoped GitHub variables for `test`.
4. Add `cd-test.yml` derived from `cd-dev.yml`, pointing at the new env.

No changes inside `iac/modules/` should be required to add an environment —
this is the property spec 003 US5 measures. For adding a new *workload*
within an existing environment (a more common operation), see § 8.

---

## 8. Adding a new workload

> Spec 003 / US5 / SC-005 — *adding* a workload (a new Container Apps Job,
> Azure Function, post-deploy probe, etc.) is module-composition only. No new
> inline `azurerm_user_assigned_identity`, `azurerm_role_assignment`,
> `azurerm_federated_identity_credential`, or
> `azuread_application_federated_identity_credential` resource blocks should
> appear in `iac/environments/<env>/main.tf`. A CI lint step
> ([`scripts/lint-iac-inline-iam.sh`](../scripts/lint-iac-inline-iam.sh), run
> by [`iac-validate.yml`](../.github/workflows/iac-validate.yml)) enforces
> this on every PR.

The identity modules under `iac/modules/` decompose by lifetime:

| Module | Lifetime | Per workload? | Purpose |
|---|---|---|---|
| `app-registration-roles` | one per env | no | Declares the four `BusTerminal.*` app roles on the API app registration. |
| `workload-identity` | one per workload | **yes** | Provisions the workload's user-assigned MI + downstream Azure RBAC (ACR pull, KV secrets, future Cosmos / AI Search / Service Bus) + any `BusTerminal.*` app-role assignments the workload calls the API with. |
| `federated-credential` | one or more per workload | **yes** (when the workload federates an external IdP, e.g., GitHub Actions) | Establishes the trust relationship between an external OIDC issuer and the workload MI. |
| `graph-permissions` | one per env | no | Declares Microsoft Graph application permissions on the API app registration. |

For most new workloads, the only module additions are `workload-identity`
and (optionally) `federated-credential`. The other two are per-environment
and already wired in `iac/environments/dev/main.tf`.

### Worked example — a discovery worker job

Walks through the minimum HCL to add a "discovery worker" Container Apps Job
to the `dev` env. It needs:

- Read on the BusTerminal API (`BusTerminal.Reader`)
- ACR pull (to fetch its image)
- Key Vault secret read (for App Insights connection string)
- GitHub Actions federation so the CD workflow can roll the job's image
  using the worker's own MI

```hcl
# iac/environments/dev/main.tf — additions only.

module "discovery_worker_identity" {
  source = "../../modules/workload-identity"

  name                = "mi-${var.naming_prefix}-discovery-worker"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  environment         = var.environment_name
  workload            = "discovery-worker"

  assigned_azure_rbac = {
    acr-pull = {
      role_definition_name = "AcrPull"
      scope                = module.container_registry.id
    }
    kv-secrets-user = {
      role_definition_name = "Key Vault Secrets User"
      scope                = module.keyvault.id
    }
  }

  api_service_principal_object_id = data.azuread_service_principal.api.object_id

  assigned_api_app_roles = {
    reader = module.app_registration_roles.role_ids.reader
  }

  tags = local.shared_tags
}

module "discovery_worker_federation" {
  source = "../../modules/federated-credential"

  name                = "github-environment-${var.environment_name}-discovery-worker"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = module.discovery_worker_identity.id
  subject             = "repo:${var.github_org_repo}:environment:${var.environment_name}"
  # issuer (GitHub Actions) + audience (Entra workload-identity) default values.
}
```

That's the full surface. Bringing this through `tofu plan` adds:

- 1 × `azurerm_user_assigned_identity` (inside the module)
- 2 × `azurerm_role_assignment` (ACR pull + KV secrets user, inside the module)
- 1 × `azuread_app_role_assignment` (API `Reader` role, inside the module)
- 1 × `azurerm_federated_identity_credential` (inside the federation module)

No new top-level inline resources — the lint guard passes.

### When the lint fails

If `scripts/lint-iac-inline-iam.sh` rejects a change, prefer one of:

1. **Compose** the resource via the appropriate module above (default path).
2. If the resource genuinely cannot be modulized (e.g., the existing
   `azurerm_role_assignment.pipeline_kv_secrets_officer` grant, which is on
   the pipeline MI authoring the env composition itself), add the
   fully-qualified address to the `ALLOWLIST` array in the script with a
   one-line comment explaining the rationale.

Per data-model.md § Federated Credential, every new federated credential
subject MUST also appear verbatim in
[`identity-and-secrets.md`](./identity-and-secrets.md) so a federation-drift
failure ("subject mismatch") can be diagnosed in one step. Update that file
in the same PR that adds the workload.

---

## 9. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `tofu init` exits with `AuthorizationFailed` against the state account | Pipeline identity lacks `Storage Blob Data Contributor` on the storage account. | Re-run platform bootstrap; verify role assignment. |
| `tofu apply` fails with `RoleAssignmentExists` | Pre-existing identical assignment from a prior manual setup. | Import the conflict (`tofu import`) or remove the manual assignment. |
| Container App revision never reaches `Healthy` | `/healthz/ready` returning 503 → backend cannot reach Entra metadata or Key Vault. | Check Container Apps logs in App Insights for the failing dependency. |
| Authenticated smoke step warns "Could not acquire API-scope token" | App-role grant in § 5 is missing. | Apply the Graph API grant once per environment. |
| Frontend reaches 200 but immediately redirects to `/signin` | The SPA app registration's redirect URI does not include the actual ingress URL (or `http://localhost:3000` for local). | Add the URL to the SPA platform's redirect URIs in Entra portal; MSAL rejects redirects to unregistered URIs. |
| Deploy wall-clock > 20 minutes | First-ever deploy (image cold cache); or large IaC plan. | Subsequent no-infra-change deploys should hit SC-002 (< 20 min). |
