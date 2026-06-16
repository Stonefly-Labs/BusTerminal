# Operator Runbook — Grant Reader on an operator-supplied Service Bus namespace

**Spec**: [008 — Namespace Onboarding](../../specs/008-namespace-onboarding/spec.md) ·
**Plan**: [plan.md](../../specs/008-namespace-onboarding/plan.md) ·
**Quickstart §5**: [quickstart.md](../../specs/008-namespace-onboarding/quickstart.md)

Before BusTerminal can onboard an Azure Service Bus namespace, its **workload User-Assigned Managed Identity (UAMI)** must hold the built-in **`Reader`** role at the namespace scope. This grant is performed by the operator out-of-band — BusTerminal does NOT modify Azure RBAC on operator-supplied resources at runtime (see Complexity Tracking #1 in the spec-008 plan).

This runbook is the authoritative procedure. It is also surfaced in the spec-008 onboarding wizard (step 1 sidebar) with the principal id and ARM scope pre-populated for copy-paste.

---

## 1. Why this is required

The spec-008 validation runner's **`RequiredPermissions`** check (FR-014) calls Azure Resource Manager (ARM) to enumerate the workload UAMI's effective permissions at namespace scope. The check passes when the action `Microsoft.ServiceBus/namespaces/read` is present in the returned `actions[]` (directly or via a wildcard like `*/read`). The simplest grant that satisfies this is the built-in **`Reader`** role at namespace scope.

If you forget this step, validation in wizard step 4 fails with `reasonCategory = ReaderRoleMissing` and onboarding is blocked (FR-023a hard-blocks `Unhealthy` aggregates; a `Degraded` aggregate from Reader-missing where Existence + Accessibility still pass via inheritance is registrable but flagged).

The Reader role GUID is `acdd72a7-3385-48ef-bd42-f606fba81ae7` (well-known Azure built-in).

---

## 2. Find BusTerminal's workload principalId

Sign in to BusTerminal as any authenticated tenant user and call the workload-identity endpoint:

```bash
TOKEN="$(your_token_acquisition)"   # any valid BusTerminal access token
curl -H "Authorization: Bearer $TOKEN" "https://{busterminal-host}/api/namespaces/identity"
```

Response shape:

```json
{
  "principalId":        "11111111-2222-3333-4444-555555555555",
  "clientId":           "66666666-7777-8888-9999-aaaaaaaaaaaa",
  "runbookUrl":         "https://github.com/Stonefly-Labs/BusTerminal/blob/main/iac/runbooks/grant-namespace-reader.md",
  "sampleGrantCommand": "az role assignment create --assignee 11111111-... --role Reader --scope {azureResourceId}"
}
```

- **principalId** — the UAMI's `principalId` (object id). This is the value you pass to `az role assignment create --assignee`.
- **clientId** — the UAMI's `clientId`. Not used here but surfaced for diagnostic purposes.
- **runbookUrl** — link back to this document.
- **sampleGrantCommand** — copy/paste template; substitute `{azureResourceId}` in step 3.

> The principalId is stable per-environment (it is the workload UAMI's Entra object id). You can cache it in your shell rather than re-fetching every onboarding.

---

## 3. Grant Reader at namespace scope

Substitute `{azureResourceId}` with the full ARM resource id of the target namespace.

```bash
PRINCIPAL_ID="11111111-2222-3333-4444-555555555555"
NAMESPACE_ARM_ID="/subscriptions/.../resourceGroups/rg-payments-prod/providers/Microsoft.ServiceBus/namespaces/orders-prod-eus2"

az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role     Reader \
  --scope    "$NAMESPACE_ARM_ID"
```

The grant takes effect within a few seconds (Azure AD propagation). The wizard step 4 retry button is the simplest way to re-validate.

> **Scope rule** — Reader at the namespace scope is sufficient. Reader at the parent (resource group, subscription, management group) scope also satisfies the check via inheritance, but is over-broad and discouraged. The `RequiredPermissions` check uses ARM's `permissions/list` API which evaluates effective permissions; inherited grants are tolerated but not preferred.

---

## 4. Verify the grant

```bash
az role assignment list \
  --assignee "$PRINCIPAL_ID" \
  --scope    "$NAMESPACE_ARM_ID" \
  --output   table
```

Expect exactly one row with `RoleDefinitionName = Reader` and `Scope = $NAMESPACE_ARM_ID`. If you see zero rows, the grant did not land — re-check the principalId and the ARM scope spelling.

Alternative verification via BusTerminal: re-run validation in the wizard step 4 (or click `Re-run validation` from the namespace details page). The `RequiredPermissions` check should now report `Pass`.

---

## 5. Rollback (revoking access)

When a namespace is decommissioned (or onboarded in error), remove the role assignment:

```bash
az role assignment delete \
  --assignee "$PRINCIPAL_ID" \
  --role     Reader \
  --scope    "$NAMESPACE_ARM_ID"
```

After rollback, the next BusTerminal validation run will report `RequiredPermissions = Fail` with `reasonCategory = ReaderRoleMissing`. The namespace's `validationStatus` becomes `Degraded` (if Existence + Accessibility still pass via inheritance) or `Unhealthy` (if not).

This does NOT delete the BusTerminal `OnboardedNamespace` document — to remove it from the active inventory, use the **Lifecycle → Archive** action from the details page. Archive is the only soft-delete path (FR-026 prohibits physical delete on the API surface).

---

## 6. Pre-go-live attestation

Before promoting BusTerminal between environments (dev → test, test → prod), capture the list of operator-supplied namespaces and their Reader grants in the deployment runbook alongside the other spec-008 attestations:

- [ ] `Group.Read.All` Graph permission admin-consented for the BusTerminal API app in this environment.
- [ ] At least one Entra principal assigned to the `Namespace Administrator` App Role in this environment.
- [ ] Reader role granted on every operator-supplied namespace this environment is expected to onboard (this runbook).

---

## 7. References

- [`specs/008-namespace-onboarding/spec.md`](../../specs/008-namespace-onboarding/spec.md) — FR-014, FR-017, FR-033, FR-043, SC-007.
- [`specs/008-namespace-onboarding/plan.md`](../../specs/008-namespace-onboarding/plan.md) — Complexity Tracking #1 (runbook-driven Reader grant).
- [`specs/008-namespace-onboarding/research.md`](../../specs/008-namespace-onboarding/research.md) §4 (declarative-IaC-vs-runbook decision) and §3 (`permissions/list` semantics).
- [`specs/008-namespace-onboarding/contracts/outputs-contract.md`](../../specs/008-namespace-onboarding/contracts/outputs-contract.md) §1.5 (runbook output declaration).
- Pipeline allowlist entry for Reader (`acdd72a7-3385-48ef-bd42-f606fba81ae7`) — `iac/platform-bootstrap/main.tf` (forward optionality for a future IaC-driven grant path).
