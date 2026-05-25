# Quickstart: Infrastructure Baseline

**Feature**: `005-infrastructure-baseline` | **Date**: 2026-05-25

Operator-facing walkthrough for applying and validating the 005 infrastructure baseline against the existing dev environment, plus the path for standing up `test` or `prod` from the templates this slice ships.

This document is the canonical "how do I run this?" reference and is updated as the implementation lands. It assumes you've already read `plan.md` and `research.md`.

---

## Prerequisites

- macOS / Linux / Windows with WSL2 (PowerShell-native Windows works for `tofu` but the policy-gate scripts require bash)
- OpenTofu `>= 1.11.0` (`tofu version` to confirm; `brew install opentofu` on macOS)
- Azure CLI `>= 2.65` (`az --version`)
- `jq` `>= 1.6` (for the policy-gate scripts)
- Git working tree on the `005-infrastructure-baseline` branch (or a topic branch off it)
- Your Entra account must hold (against the dev tenant `596c1564-6e95-4c35-a80b-2dbe45a162f3`):
  - Membership in the `KV_OPERATOR_OBJECT_IDS_DEV` GitHub variable list (or be added — see §A.4)
  - `Contributor` on the dev RG `rg-bt-dev` (for local applies; CI uses the pipeline UAMI)
  - `Cosmos DB Built-in Data Contributor` on the dev Cosmos account (auto-granted by the dev composition when `data.azurerm_client_config.current` resolves to your principal)

---

## A. Apply the baseline against dev (incremental)

Spec 005 is a selective retrofit (Q2c). After implementation, the apply path is:

### A.1 Authenticate

```bash
az login
az account set --subscription 08b37dc0-0011-4841-84c0-0349a5c65883
```

### A.2 Initialize OpenTofu

```bash
cd iac/environments/dev
tofu init   # honors .terraform.lock.hcl; no -upgrade
```

If the lockfile has drifted on your machine (e.g., you previously ran with a different provider version), the `init` will fail; resolve by:
```bash
tofu init -upgrade   # only if you intend to update the lockfile — and then commit the change
```

### A.3 Plan

```bash
tofu plan -var-file=terraform.tfvars -out=tfplan
```

The plan should show:
- **Adds** for the new resources this slice introduces (VNet, subnets, private DNS zones, AI Search, Service Bus namespace, private endpoints for Cosmos / KV / AI Search, new role assignments per FR-033, new diagnostic-settings module instances for net-new resources)
- **Changes** for refactors to existing resources (the `monitoring` module's retention as a tf-var; the workload UAMI's new role-assignment map entries; the App Insights `local_authentication_disabled` explicit setting)
- **Removes** for ONLY the two `enabled_metric { category = "AllMetrics" }` blocks on the Container App diagnostic settings (per Q5c)
- **Imports** for the `import {}` blocks that adopt manually-created 002-era role assignments and any net-new adoption (none expected if the existing imports are still intact)
- **Zero destroys** against any stateful resource listed in `data-model.md` §3

If the plan shows a destroy against a stateful resource, STOP. Investigate. Do not apply.

### A.4 Run the policy gate locally

```bash
tofu show -json tfplan > tfplan.json
bash ../../policies/run-policies.sh --plan tfplan.json --env dev --allowlist ../../policies/allowlist.json
```

All rules from `contracts/policy-rules.md` should pass. If any fail, fix in code (do NOT add allowlist entries casually).

### A.5 Apply

```bash
tofu apply tfplan
```

Expected duration: **15–30 minutes** for a full plan covering this slice's adds. Private endpoint provisioning + DNS-zone-link propagation dominates the runtime.

### A.6 Validate

After apply:
```bash
tofu output -json | jq .
```

Confirm presence and shape of every output declared in `contracts/outputs-contract.md`. Confirm `application_insights_connection_string` is marked sensitive (its value is not echoed by the JSON output).

Run the live dev smoke (existing from spec 002):
```bash
# From repo root:
gh workflow run cd-dev.yml   # or watch the existing CD pipeline
# Confirm: backend /healthz returns 200, frontend renders, MSAL sign-in still works
```

If the dev URL changed or sign-in is broken, STOP and roll back — something destroyed the CAE or KV when it shouldn't have.

---

## B. Stand up test or prod from the template

The templates this slice ships are NOT applied automatically (Q1c env scope = dev only). When ready to stand up `test` or `prod`:

### B.1 Extend the bootstrap stack

`iac/platform-bootstrap/terraform.tfvars` adds the new env to the list:
```hcl
environments = ["dev", "test", "prod"]   # was ["dev"]
github_org_repo = "Stonefly-Labs/BusTerminal"
```

Apply:
```bash
cd iac/platform-bootstrap
tofu init
tofu plan -out=tfplan
tofu apply tfplan
```

This creates `mi-busterminal-pipeline-test` and `mi-busterminal-pipeline-prod` UAMIs federated to the corresponding GitHub environments, with the same condition-scoped RBAC-Admin grant (now extended per research §12). Federated-credential subjects are `repo:Stonefly-Labs/BusTerminal:environment:test` and `:prod`.

### B.2 Create GitHub environments

In the GitHub repo settings, create `test` and `prod` environments. Each should have:
- A required reviewer for deployments
- A deployment-protection rule for protected branches (`main` only for `prod`; broader for `test`)
- An environment variable `KV_OPERATOR_OBJECT_IDS_TEST` (or `_PROD`) with the same JSON-list shape as `_DEV`
- A repo-level Entra app registration for the env's identity if Entra apps are per-env (existing pattern is one set for dev — a per-env decision when test/prod stand up)

### B.3 Populate `terraform.tfvars`

Copy from `iac/environments/<env>/terraform.tfvars.example` and fill in the real values:
- `subscription_id` — the env's target subscription
- `unique_suffix` — unique to this env's region; check Azure for naming collisions on KV / ACR / Cosmos
- `entra_tenant_id` / `entra_*_client_id` — env-specific app registrations
- `platform_role_ids` — generated with `uuidgen` (or reused if reusing the same Entra app)

Per the template defaults: test/prod default to `data_services_public_access_enabled = false`, Premium SB SKU, KV purge protection ON, 90-day KV soft-delete retention.

### B.4 Plan and apply

```bash
cd iac/environments/<env>
tofu init -backend-config="key=envs/<env>/terraform.tfstate"
tofu plan -var-file=terraform.tfvars -out=tfplan
tofu show -json tfplan > tfplan.json
bash ../../policies/run-policies.sh --plan tfplan.json --env <env>
tofu apply tfplan
```

Expected first-apply duration for a fresh env: **45–60 minutes** (creating everything from empty).

---

## C. CI / GitHub Actions

After this slice merges, two workflows govern infra changes:

### `iac-validate.yml` (PR gate)

Triggers: PR to any branch touching `iac/**`.

Steps:
1. Checkout
2. Set up OpenTofu + jq
3. For each env composition (`dev`, `test`, `prod`):
   - `tofu init -backend=false` (no state read needed for validate)
   - `tofu validate`
4. For dev (the only env with state):
   - Federated-credential login via the pipeline UAMI
   - `tofu init` (with backend)
   - `tofu plan -var-file=terraform.tfvars -out=tfplan`
   - `tofu show -json tfplan > tfplan.json`
   - `bash iac/policies/run-policies.sh --plan tfplan.json --env dev`
5. Run `checkov --framework terraform_plan -f tfplan.json` (or against the source HCL)
6. Run `tfsec iac/`
7. Post plan summary to PR via `gh pr comment`
8. Mark check as REQUIRES MANUAL APPROVAL if `BT-IAC-007` flags a stateful destroy

### `iac-apply-dev.yml` (CD)

Triggers: push to `main` AND any change under `iac/environments/dev/**` or `iac/modules/**` or `iac/platform-bootstrap/**`.

Steps:
1. Re-run the validate workflow's plan
2. On approval (or auto for non-destructive changes): `tofu apply tfplan`
3. Post apply summary to a deployment-events channel (Slack/Teams if wired; otherwise GitHub Deployments API)

`iac-apply-test.yml` and `iac-apply-prod.yml` are added when their environments stand up; the pattern is identical with stricter approval gating on prod.

---

## D. Troubleshooting common issues

### "Error: code=AuthorizationFailed" during apply

The pipeline UAMI or your local Entra account is missing a role. Check:
- For `azurerm_role_assignment` writes: the pipeline UAMI's RBAC-Admin condition allowlist (research §12) — does it include the role GUID you're trying to assign?
- For `azurerm_key_vault_secret`: KV Secrets Officer scoped to either the env RG (pipeline) or the vault (operators)
- For data-plane Cosmos writes: Cosmos DB Built-in Data Contributor on the database

### "Error: A resource with the ID ... already exists"

The resource was created out-of-band. Adopt it via an `import {}` block matching the existing dev pattern (e.g., lines 114–122 of `iac/environments/dev/main.tf`).

### Plan shows a destroy on the Container Apps Environment

This is the destructive retrofit that Q2c explicitly defers. STOP. Investigate why the plan wants to destroy it — likely an unintended attribute change in the `container-apps-env` module. The retrofit is its own future slice; do not apply this destroy in spec 005's scope.

### Plan shows a destroy on `azurerm_key_vault_secret.app_insights_connection_string`

This breaks the Container Apps secret reference at the next CAE restart. STOP. Likely cause: a change to the secret's `name`, `key_vault_id`, or a change that triggers a force-new. Fix in code; if you genuinely need to rotate the secret, do it via a separate PR with an explicit "ApplicationInsightsConnectionString rotation" justification and coordinate the Container Apps restart.

### Browser telemetry stops appearing in Application Insights

Check that `local_authentication_disabled = false` on the App Insights resource (research §6, Q1c). If it got flipped to `true`, the JavaScript SDK's ingestion-key-based requests start 401-ing. The CI policy gate's `BT-IAC-003`-adjacent rule (or a follow-up rule) should prevent this — confirm.

### `BT-IAC-002` fires in dev

Q2c explicitly allows dev to have `data_services_public_access_enabled = true`. The rule is environment-conditional — it only fires for `environment_name` starting with `prod`. If it fires in dev, the rule's env-check is broken; debug `iac/policies/check-public-access.sh`.

### `BT-IAC-007` fires on a tfplan that shouldn't be destructive

A non-trivial module change can ripple to a destroy. Read the plan carefully — the offending resource is named in the failure message. If the destroy IS intentional (rare), the PR must:
1. Add an allowlist entry to `iac/policies/allowlist.json` with a justification
2. Get a maintainer reviewer to approve the manual-approval gate

---

## E. Reverting a bad apply

OpenTofu state lives at `https://btstatech0001.blob.core.windows.net/tfstate/envs/dev/terraform.tfstate`. Blob versioning is ON; previous versions are recoverable via:

```bash
az storage blob list --account-name btstatech0001 \
  --container-name tfstate --prefix envs/dev/ \
  --include v --auth-mode login
# Copy a specific version blob back to the current name via az storage blob copy or the portal UI
```

If a destructive apply landed and resources were deleted:
- KV is soft-deleted (7-day window in dev) — recover via `az keyvault recover`
- Cosmos DB is soft-deleted (30-day window by default) — recover via `az cosmosdb restore`
- Container Apps Environment / Container Apps cannot be recovered; redeploy from current code

Roll-forward is preferred to roll-back: fix the code, re-plan, re-apply.

---

## F. Future-spec hooks

These outputs are reserved for downstream specs to consume:

| Output | Downstream consumer |
|---|---|
| `service_bus_namespace_id` + `service_bus_namespace_fqdn` | Spec 006+ messaging topology (creates topics/queues against this namespace) |
| `ai_search_id` + `ai_search_endpoint` | Spec 008+ search projection (creates indexes via the workload UAMI's `Search Index Data Contributor` role) |
| `cosmos_canonical_database_role_scope` | Any future spec adding a Cosmos role assignment (use the exact scope path to avoid the ARM-vs-data-plane trap) |
| `private_dns_zone_ids` map | Specs adding new private endpoints to existing services |
| `subnet_private_endpoints_id` | Same |
| `subnet_integration_id` | The destructive-retrofit follow-up spec (CAE VNet integration) |

If a downstream spec needs an output not on this list, the spec's plan should add it to `outputs-contract.md` and to `iac/environments/<env>/outputs.tf`.
