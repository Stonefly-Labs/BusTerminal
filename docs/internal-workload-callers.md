# BusTerminal — Internal Workload Callers

**Status**: Authoritative · **Slice**: 003-auth-and-identity · **Spec**: [specs/003-auth-and-identity/spec.md](../specs/003-auth-and-identity/spec.md) (FR-012, FR-022, SC-003)

This document is the recipe for "I have a new internal workload (Container Apps Job, Functions container, sidecar) that needs to call the BusTerminal API." It is the durable home for the pattern proven by spec 003 user story 3.

The rule, in one sentence: **internal workloads authenticate to the BusTerminal API with their managed identity, presenting a normal Entra-issued bearer token — there is no internal-trust bypass, no shared secret, no `X-Internal-Caller` header (FR-012).**

If you want a one-screen smoke that this works today, see the worked example at the bottom against `mi-bt-dev-workload`.

For broader credential context (MSAL, OIDC federation, `DefaultAzureCredential`), see [`identity-and-secrets.md`](./identity-and-secrets.md). For the Entra role-administration runbook, see the Operator runbook in [`quickstart.md`](../specs/003-auth-and-identity/quickstart.md) Part B (will be promoted to `docs/identity-role-administration.md` in Phase 9 polish).

---

## The shape of an internal call

```
┌──────────────────────────┐                       ┌──────────────────────┐
│ Internal workload        │                       │ BusTerminal API      │
│ (Container Apps Job /    │                       │ (ca-bt-<env>-api)    │
│  Functions / sidecar)    │                       │                      │
│                          │                       │                      │
│ az get-access-token \    │ Authorization: Bearer │ Microsoft.Identity.  │
│   --resource             │ ─────────────────────▶│ Web validates token  │
│   api://<api-app>/.default              (Entra)  │ → CallerType.Workload│
│                          │                       │ → roles claim →      │
│                          │                       │   RolePolicies       │
│   (uses workload MI)     │                       │ → 200 / 401 / 403    │
└──────────────────────────┘                       └──────────────────────┘
```

The same authorization pipeline that handles human callers handles workloads. The only externally-visible differences are:

| Concern | Human | Workload |
|---|---|---|
| `idtyp` claim | absent (or `user`) | `app` |
| `name` / `preferred_username` claims | populated | absent |
| `PlatformPrincipal.CallerType` | `Human` | `Workload` |
| `roles` claim | granted via Enterprise Apps → Users and groups | granted via `azuread_app_role_assignment` in IaC |
| Token acquisition | MSAL (Authorization Code + PKCE) | `DefaultAzureCredential` / `az get-access-token` (Managed Identity) |

The role-permission matrix evaluates identically for both. If a workload holds only `BusTerminal.Reader`, it can call `/probe/read` and gets `403` on `/probe/administer` — exactly like a human.

---

## Pattern: add a new internal-caller workload

### Step 1 — provision the workload identity

Add a `module "workload_identity"` block to `iac/environments/<env>/main.tf`. The module provisions the user-assigned managed identity, attaches optional API app-role assignments (so it can call the BusTerminal API), and optionally grants Azure-resource RBAC on downstream services.

```hcl
# iac/environments/dev/main.tf — add alongside the existing modules

module "workload_identity_discovery_worker" {
  source = "../../modules/workload-identity"

  name                = "mi-bt-dev-discovery-worker"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  environment         = var.environment_name
  workload            = "discovery-worker"

  # Downstream Azure RBAC (data-plane access on whatever the workload reads).
  assigned_azure_rbac = {
    kv-secrets-user = {
      role_definition_name = "Key Vault Secrets User"
      scope                = module.keyvault.id
    }
  }

  # API authorization — grant the workload the smallest role that satisfies
  # its operation classes. `Reader` is the right default for a workload that
  # only consumes the read surface; use `Operator` for a workload that issues
  # `MutateDomain` / `OperatePlatform` calls; never grant `Admin` to a
  # workload (FR-002a equivalent — Admin grants are a human action).
  api_service_principal_object_id = data.azuread_service_principal.api.object_id

  assigned_api_app_roles = {
    reader = module.app_registration_roles.role_ids.reader
  }

  tags = local.shared_tags
}
```

The module's `name` regex requires `mi-bt-<env>-<workload>`. Lifecycle expectations:

- `principal_id` is the value carried as `PlatformPrincipal.ObjectId` on workload-issued tokens — record it if you need to correlate with backend audit logs (FR-032).
- `client_id` is what `DefaultAzureCredential` consumes via `AZURE_CLIENT_ID` to disambiguate when multiple MIs are attached.
- The `azuread_app_role_assignment` resources have eventual-consistency propagation through Entra. First-deploy workloads typically see the role appear within 1–3 minutes; a `time_sleep` between the role assignment and the workload's `azurerm_container_app*` resource is unnecessary in practice but can be added if a job runs immediately after provisioning.

### Step 2 — attach the MI to the workload container

For a Container Apps Job:

```hcl
resource "azurerm_container_app_job" "discovery_worker" {
  # ... name, location, container_app_environment_id, etc.

  identity {
    type         = "UserAssigned"
    identity_ids = [module.workload_identity_discovery_worker.id]
  }

  template {
    container {
      # ...
      env {
        name  = "AZURE_CLIENT_ID"
        value = module.workload_identity_discovery_worker.client_id
      }
      env {
        name  = "BUSTERMINAL_API_URL"
        value = "https://${module.backend_app.fqdn_url}"
      }
      env {
        name  = "BUSTERMINAL_API_SCOPE"
        value = "api://${var.entra_api_client_id}/.default"
      }
    }
  }
}
```

The Container App / Function variant is identical except for the resource type.

### Step 3 — acquire and use the token

Inside the workload (any language with Azure SDK or `az` CLI access):

**Bash / sh**:

```sh
az login --identity --client-id "$AZURE_CLIENT_ID" > /dev/null
TOKEN=$(az account get-access-token --resource "$BUSTERMINAL_API_SCOPE" --query accessToken -o tsv)
curl -sS -H "Authorization: Bearer $TOKEN" "$BUSTERMINAL_API_URL/probe/read"
```

**.NET (the same factory the backend uses):**

```csharp
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"),
});

var token = await credential.GetTokenAsync(
    new TokenRequestContext(new[] { Environment.GetEnvironmentVariable("BUSTERMINAL_API_SCOPE")! }),
    cancellationToken);

httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token.Token);
```

**Python**:

```python
from azure.identity import DefaultAzureCredential
cred = DefaultAzureCredential(managed_identity_client_id=os.environ["AZURE_CLIENT_ID"])
token = cred.get_token(os.environ["BUSTERMINAL_API_SCOPE"]).token
```

### Step 4 — observe the call

A successful call surfaces in App Insights with `idtyp=app` and `CallerType=Workload` recorded on the projected `PlatformPrincipal`. An unauthorized call surfaces as a 403 with a structured log line carrying `caller_oid`, `caller_effective_roles`, `required_operation_class`, and `required_roles` (FR-032 / FR-033 — token contents are NOT logged).

---

## Worked example: `mi-bt-dev-workload` → `/probe/read`

The dev environment ships with `mi-bt-dev-workload` already provisioned (see `iac/environments/dev/main.tf`). Spec 003 grants it `BusTerminal.Reader` on the API via the `workload-identity` module, so it should be able to call `/probe/read` with no additional configuration.

There is an opt-in probe Container Apps Job (`iac/modules/probe-job-internal-caller/`, off by default) that bundles steps 2-3 into a single re-runnable smoke. To enable it:

```hcl
# iac/environments/dev/terraform.tfvars OR via -var
probe_job_enabled = true
```

Then `tofu apply`. To execute the job:

```sh
az containerapp job start \
  --name caj-bt-dev-probe-internal-caller \
  --resource-group rg-bt-dev
```

The job's container logs (`az containerapp job logs show --name caj-bt-dev-probe-internal-caller --resource-group rg-bt-dev`) should print `probe status: 200` and exit 0. A non-zero exit means the workload's role assignment hasn't propagated yet, the role is wrong, or the API audience is misconfigured — read the body of the response in the log to disambiguate.

To verify the negative case, temporarily remove the `reader` entry from `assigned_api_app_roles`, re-apply, re-run the job. The job should print `probe status: 403` and exit 1.

---

## Anti-patterns to avoid

- **Do not invent a header like `X-Internal-Caller: true` to short-circuit auth.** FR-012 explicitly prohibits internal-trust bypass.
- **Do not share secrets between workloads.** Each workload has its own MI; cross-workload calls go through the same Entra issuance path as everything else.
- **Do not grant a workload `BusTerminal.Admin`.** Admin is intentionally a human role (FR-002a). If a workload genuinely needs to perform admin-class operations, that's an architectural smell — talk to the platform owner.
- **Do not catch and swallow 401/403 from the API.** If the workload's call is rejected, fail loudly so the role-misconfiguration is visible. The 403 problem-details body names the `requiredOperationClass` and `requiredRoles` — surface them in your workload's error log.

---

## Cross-references

- [`identity-and-secrets.md`](./identity-and-secrets.md) — the four credential mechanisms in BusTerminal
- [`local-development.md`](./local-development.md) — running the local stack (humans only; workloads run on Azure)
- [`deploying-environments.md`](./deploying-environments.md) — how the pipeline reaches Azure
- [`../specs/003-auth-and-identity/contracts/role-permission-matrix.md`](../specs/003-auth-and-identity/contracts/role-permission-matrix.md) — the matrix every workload's role assignment must satisfy
- [`../specs/003-auth-and-identity/quickstart.md`](../specs/003-auth-and-identity/quickstart.md) § Part D · SC-003 — the smoke recipe this document elaborates
