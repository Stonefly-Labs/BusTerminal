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
- **Frontend (web) registration** (NextAuth.js initiates sign-in here)
  - Redirect URI: `https://<frontend-fqdn>/api/auth/callback/microsoft-entra-id`
  - A client secret stored in Key Vault as `WebClientSecret`
  - API permission: `access_as_user` on the backend API registration, admin consented

A separate `docs/identity-and-secrets.md` (slice US4) will walk through
creating these. For US2, the only requirement is that the resulting client IDs
are configured as environment variables above and that `WebClientSecret` and
`NextAuthSecret` exist in the environment's Key Vault.

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
identity to be authorized as a client of the backend API. This is a one-time
configuration per environment:

1. In Entra ID → app registrations → **backend API registration** →
   **Expose an API**, ensure `access_as_user` is defined (default).
2. Create an app role on the backend registration (e.g.,
   `Smoke.Invoke`, type `Application`, value `Smoke.Invoke`).
3. Grant the pipeline managed identity (the service principal whose object ID
   matches `principalId` on `mi-busterminal-pipeline-dev`) that app role
   via Microsoft Graph:

```sh
PIPELINE_SP_OBJECT_ID=$(az ad sp show --id <pipeline-mi-client-id> --query id -o tsv)
BACKEND_SP_OBJECT_ID=$(az ad sp show --id <backend-api-client-id> --query id -o tsv)
APP_ROLE_ID=$(az ad sp show --id <backend-api-client-id> --query "appRoles[?value=='Smoke.Invoke'].id" -o tsv)

az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${PIPELINE_SP_OBJECT_ID}/appRoleAssignments" \
  --headers "Content-Type=application/json" \
  --body "{
    \"principalId\": \"${PIPELINE_SP_OBJECT_ID}\",
    \"resourceId\": \"${BACKEND_SP_OBJECT_ID}\",
    \"appRoleId\": \"${APP_ROLE_ID}\"
  }"
```

Until this grant is in place, the authenticated smoke step is `continue-on-error`
and reports a warning rather than failing the deploy. The unauthenticated
smoke step still validates the security posture.

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

## 7. Adding a new environment (US5 preview)

The pattern is documented in [`iac/environments/test/README.md`](../iac/environments/test/README.md)
(US5 deliverable). The summary:

1. Re-run the bootstrap with the additional environment name (`-e test`) so
   the matching managed identity + federated credential is created.
2. Copy `iac/environments/dev/*` to `iac/environments/test/`, renaming the
   backend key (`envs/test/terraform.tfstate`).
3. Configure environment-scoped GitHub variables for `test`.
4. Add `cd-test.yml` derived from `cd-dev.yml`, pointing at the new env.

No changes inside `iac/modules/` should be required to add an environment —
that is the test US5 measures.

---

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `tofu init` exits with `AuthorizationFailed` against the state account | Pipeline identity lacks `Storage Blob Data Contributor` on the storage account. | Re-run platform bootstrap; verify role assignment. |
| `tofu apply` fails with `RoleAssignmentExists` | Pre-existing identical assignment from a prior manual setup. | Import the conflict (`tofu import`) or remove the manual assignment. |
| Container App revision never reaches `Healthy` | `/healthz/ready` returning 503 → backend cannot reach Entra metadata or Key Vault. | Check Container Apps logs in App Insights for the failing dependency. |
| Authenticated smoke step warns "Could not acquire API-scope token" | App-role grant in § 5 is missing. | Apply the Graph API grant once per environment. |
| Frontend reaches 200 but redirects loop | `NEXTAUTH_URL` does not match the actual ingress URL. | Verify the Container App FQDN in `outputs.tf` matches what NextAuth.js expects. |
| Deploy wall-clock > 20 minutes | First-ever deploy (image cold cache); or large IaC plan. | Subsequent no-infra-change deploys should hit SC-002 (< 20 min). |
