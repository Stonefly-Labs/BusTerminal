# Quickstart — Namespace Onboarding (Spec 008)

**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md) · **Data Model**: [data-model.md](./data-model.md)

This is the operator + developer walkthrough for the spec-008 namespace onboarding feature. It covers (1) what to deploy, (2) what to admin-consent, (3) how to grant Reader to BusTerminal's workload identity on an operator-supplied namespace, (4) how to drive the wizard, and (5) how to verify everything end-to-end.

---

## 1. Prerequisites

Before this slice can be used in any environment:

1. **Specs 001 — 007 are deployed and healthy** in the target environment. Spec 008 is purely additive on top of spec 006's Registry Core and spec 003's identity foundation.
2. **An Azure Service Bus namespace exists** that the operator wants to onboard (BusTerminal does NOT create namespaces; spec Non-Goal).
3. **An Entra tenant administrator is available** to (a) admin-consent the new `Group.Read.All` Graph permission, (b) assign at least one user/group to the new `Namespace Administrator` App Role.
4. **The operator has the Entra-side ability to grant Reader role** on the target Service Bus namespace (typically: Owner / User Access Administrator at namespace or RG or subscription scope).

---

## 2. Deploy the IaC changes

From `iac/`:

```bash
# (in a feature branch / PR)
tofu fmt
tofu init -upgrade=false                     # locks must match
tofu plan -var-file=environments/dev/dev.tfvars   # review plan
tofu apply -var-file=environments/dev/dev.tfvars  # apply
```

Expected plan summary:
- `module.cosmos_registry_store.azurerm_cosmosdb_sql_container.containers["namespace-validation-runs"]` — create
- `module.app_registration_roles.azuread_application_app_role.this["namespace-administrator"]` — create
- `module.graph_permissions.azuread_application_api_access.this["graph"].permissions[...]` — add Group.Read.All
- `azurerm_role_assignment.pipeline_role_admin` — update (condition v2.0 — adds Reader GUID to allowlist)

**Zero new Azure resource modules; zero new Container Apps; zero new Cosmos accounts; zero new identity resources.**

The CI pipeline's BT-IAC-001..007 gates must continue to PASS. If BT-IAC-004 fails, double-check that no spec-008 resource grants subscription-wide RBAC.

---

## 3. Admin-consent the new Graph permission

After IaC apply, an Entra tenant administrator MUST grant admin consent for `Group.Read.All` on the BusTerminal API app:

```bash
# Get the BusTerminal API app id from the IaC output:
APP_ID=$(tofu -chdir=iac/environments/dev output -raw api_app_application_id)

# Grant admin consent (interactive — requires tenant-admin signed in to az CLI):
az ad app permission admin-consent --id "$APP_ID"
```

Verify:

```bash
az ad app permission list-grants --id "$APP_ID" --output table
# Expect to see both User.Read.All (existing) and Group.Read.All (new).
```

**Capture this attestation** in the deployment runbook under "Pre-go-live attestations — Graph permissions" alongside the spec-003 `User.Read.All` entry.

---

## 4. Assign the new `Namespace Administrator` App Role

After IaC apply (the role is *defined* but unassigned), an Entra tenant administrator MUST assign at least one user or group to it via the Enterprise App page (BusTerminal API app → Users and groups → Add user/group → Namespace Administrator):

```bash
# Or via CLI — find the role objectId and the principal objectId, then:
az rest --method post \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/{enterpriseAppOid}/appRoleAssignments" \
  --body '{
    "principalId": "{userOrGroupObjectId}",
    "resourceId": "{enterpriseAppOid}",
    "appRoleId": "{namespaceAdminRoleId}"
  }'
```

Smoke-check: any user with the role assigned should receive a `BusTerminal.NamespaceAdministrator` claim in their JWT on next sign-in. Confirm via `GET /api/identity/whoami` (existing spec-003 endpoint) — `effectiveRoles` should include `NamespaceAdministrator`.

---

## 5. Grant Reader role on operator-supplied namespaces (runbook)

> This is the operator runbook also published at `iac/runbooks/grant-namespace-reader.md` and surfaced in the wizard step 1 sidebar.

For each Azure Service Bus namespace an operator intends to onboard, BusTerminal's workload UAMI needs the built-in **`Reader`** role at the namespace scope.

### 5.1 Find BusTerminal's workload principalId

Sign in to BusTerminal as any authenticated user and call:

```bash
curl -H "Authorization: Bearer $(your_token)" https://{busterminal-host}/api/namespaces/identity
```

Response:

```json
{
  "principalId": "11111111-2222-3333-4444-555555555555",
  "clientId":    "66666666-7777-8888-9999-aaaaaaaaaaaa",
  "runbookUrl":  "https://github.com/Stonefly-Labs/BusTerminal/blob/main/iac/runbooks/grant-namespace-reader.md",
  "sampleGrantCommand": "az role assignment create --assignee 11111111-... --role Reader --scope {azureResourceId}"
}
```

### 5.2 Grant Reader on the target namespace

```bash
# Substitute {azureResourceId} with the ARM id of the target namespace.
az role assignment create \
  --assignee "11111111-2222-3333-4444-555555555555" \
  --role Reader \
  --scope "/subscriptions/.../Microsoft.ServiceBus/namespaces/orders-prod-eus2"
```

### 5.3 Verify

```bash
az role assignment list \
  --assignee "11111111-..." \
  --scope    "/subscriptions/.../Microsoft.ServiceBus/namespaces/orders-prod-eus2" \
  --output table
# Expect one row with role 'Reader' and scope = the namespace.
```

### 5.4 Rollback (if needed)

```bash
az role assignment delete \
  --assignee "11111111-..." \
  --role Reader \
  --scope "/subscriptions/.../Microsoft.ServiceBus/namespaces/orders-prod-eus2"
```

After rollback, the next BusTerminal validation run for that namespace will report `RequiredPermissions` = `Fail` with `reasonCategory = ReaderRoleMissing`. The namespace's `validationStatus` will become `Degraded` if Existence + Accessibility still pass (i.e., Reader at parent scope), otherwise `Unhealthy`.

---

## 6. Onboard your first namespace (the wizard)

Once §3, §4, and §5 are done for one target namespace:

1. Sign in to BusTerminal as a user holding the `Namespace Administrator` role.
2. Click **Namespaces** in the top nav → click **Onboard a namespace**.
3. **Step 1 — Identification**: paste the ARM resource id. The wizard parses it and pre-validates:
   - Format (`/subscriptions/.../Microsoft.ServiceBus/namespaces/{name}`).
   - Cross-tenant guard (subscription's `tenantId` matches BusTerminal's configured tenant id — server-side authoritative, frontend advisory).
   - Already-onboarded guard (case-insensitive ARM id match).
   - The sidebar surfaces the `az role assignment create` block pre-populated with the workload UAMI principalId and the pasted ARM id (from `GET /api/namespaces/identity`).
4. **Step 2 — Metadata**: capture display name (defaults from namespace name), description, environment classification, business unit, product/application, cost center, tags, notes.
5. **Step 3 — Ownership**: assign a primary owner (required) and optional secondary owners, technical stewards, support contacts. The Entra picker searches BusTerminal's tenant via Microsoft Graph (`User.Read.All` + `Group.Read.All`).
6. **Step 4 — Validation**: click **Run validation**. The five checks (Existence, Accessibility, RequiredPermissions, IdentityAuthorization, ApiReachability) run in parallel; per-check progress is shown. Expected p95 < 15s; per-check hard timeout 5s.
   - If aggregate `Healthy` or `Degraded`: the Register button becomes enabled.
   - If aggregate `Unhealthy` (Existence or Accessibility fail): Register is disabled. The wizard surfaces remediation hints (most commonly: the `Reader` role isn't granted — surface the §5.2 command).
7. **Step 5 — Review & Register**: review the captured details, click **Register**. The OnboardedNamespace document is persisted, the audit event is emitted, the namespace appears in the Inventory immediately, and the wizard's transient sessionStorage state is cleared.

Total time, first-onboard end-to-end on a clean account: **under 5 minutes** (per SC-001).

---

## 7. Verify the slice end-to-end

### 7.1 API smoke checks

```bash
# 1. Identity endpoint returns the workload UAMI principalId
curl -H "Authorization: Bearer $(your_token)" https://{host}/api/namespaces/identity

# 2. Pre-onboarding validation against a real namespace
curl -X POST -H "Authorization: Bearer $(admin_token)" \
     -H "Content-Type: application/json" \
     -d '{"azureResourceId":"/subscriptions/.../Microsoft.ServiceBus/namespaces/orders-prod-eus2"}' \
     https://{host}/api/namespaces/_validate

# 3. After step-5 register, list inventory
curl -H "Authorization: Bearer $(your_token)" https://{host}/api/namespaces

# 4. Get details
curl -H "Authorization: Bearer $(your_token)" https://{host}/api/namespaces/{id}

# 5. Re-run validation
curl -X POST -H "Authorization: Bearer $(admin_token)" https://{host}/api/namespaces/{id}/validation-runs

# 6. Transition lifecycle to Disabled
curl -X POST -H "Authorization: Bearer $(admin_token)" \
     -H "Content-Type: application/json" \
     -H "If-Match: $ETAG" \
     -d '{"action":"disable","reason":"Planned decommission window"}' \
     https://{host}/api/namespaces/{id}/lifecycle
```

### 7.2 Frontend smoke checks

Open `/namespaces` in a browser as an authenticated user:
- Empty state on a fresh deploy.
- After onboarding one namespace via §6, the inventory table shows it with `Active` / `Healthy` badges.
- The details page renders all fields, the validation panel shows the five-check breakdown, the audit panel shows the `NamespaceOnboarded` event.
- The lifecycle action dialog accepts a reason note and transitions through Disabled / Enable / Archive / Restore as expected.

### 7.3 Telemetry smoke checks

In Azure Monitor → Application Insights → Transaction Search:
- Search for `namespace.onboarding.run` — every step-5 register creates one parent span tree with 5 child `namespace.validation.check.*` spans.
- Search for `namespace.lifecycle.transition` — every lifecycle action creates a span.
- Search for `403` responses on `/api/namespaces/*` — every authorization rejection should appear here with the actor objectId attribute set.

### 7.4 Accessibility smoke checks

In the local dev frontend (`pnpm dev` in `web/`):
- Run `pnpm test:a11y` — axe-playwright suite for `tests/a11y/namespaces/*` must report zero violations.
- Manual: navigate the wizard with keyboard only (Tab/Shift-Tab/Enter); each step's focus management should be clear and visible.

---

## 8. Local development setup

No new tooling. Existing project setup applies:

```bash
# Backend
cd api && dotnet restore && dotnet build && dotnet test

# Frontend
cd web && pnpm install && pnpm dev   # http://localhost:3000
# In another shell:
cd api/BusTerminal.Api && dotnet run    # http://localhost:5001
```

To run validation against a real Azure namespace in local dev, set:

```bash
export BUSTERMINAL_TEST_ARM_NAMESPACE_ID="/subscriptions/.../Microsoft.ServiceBus/namespaces/your-test-ns"
```

And ensure your local `az login` session has Reader on that namespace (the local dev flow uses `DefaultAzureCredential` which picks up the local CLI credential when running outside Azure).

---

## 9. Frequently-encountered errors and remediations

| Symptom | Likely cause | Fix |
|---|---|---|
| Wizard step 1 rejects ARM id with "Expected Microsoft.ServiceBus/namespaces" | Wrong resource type | Paste the namespace-level ARM id, not a queue/topic/sub. |
| Wizard step 1 rejects with "namespace tenant differs from BusTerminal tenant" | Cross-tenant onboarding (FR-006) | Out of scope for v1; use a namespace in BusTerminal's tenant. |
| Step-4 `RequiredPermissions` fails with `ReaderRoleMissing` | §5 not run for this namespace | Run the `az role assignment create` command from the wizard sidebar. |
| Step-4 `Accessibility` fails with `ArmAccessDenied` | Reader not even at parent scope | Same as above (Reader at namespace scope is sufficient). |
| Step-4 `IdentityAuthorization` fails with `TokenExchangeFailed` | Transient Entra issue OR workload UAMI federation broken | Retry; if persistent, check the Container Apps revision's identity bindings. |
| Step-5 register returns 409 "ValidationRun stale" | More than 30 minutes elapsed between step 4 and step 5 | Re-run validation in step 4. |
| `POST /api/namespaces/{id}/metadata` returns 412 PreconditionFailed | If-Match ETag is stale | Re-fetch the document via GET, retry with the fresh ETag. |
| Inventory missing the newly-onboarded namespace | Browser stale cache | Hard reload — the inventory is served from the persistent store, not the AI Search index, so there is no indexing lag (FR-021). |

---

## 10. Coordinating with spec 006 (legacy Manual namespaces)

If a target namespace was already registered manually via spec 006 (`source = Manual`), the spec-008 onboarding endpoint will reject it as "already exists" via FR-007 ARM-id duplicate detection — even though the document was created by a different code path. The recommended migration path (NOT in this slice's scope) is a future "registry-domain unification" spec that walks Manual namespaces and promotes them to Onboarded by running validation against their `azureResourceId`. Until then, Manual namespaces remain readable and writable through spec-006's polymorphic API; only spec-008's writes are gated by the new `namespace-administrator` role.

---

Spec 008 is now ready for `/speckit-tasks`.
