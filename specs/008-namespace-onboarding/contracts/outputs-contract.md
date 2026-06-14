# Outputs Contract — Namespace Onboarding (Spec 008)

**Plan**: [../plan.md](../plan.md) · **Spec**: [../spec.md](../spec.md) · **Research**: [../research.md](../research.md)

This document enumerates the incremental outputs and attestations introduced by spec 008. No new Azure resources are introduced; all deltas are inputs to existing IaC modules + one operator runbook + one App Role definition + one Graph permission consent. Each item identifies the module touched, the contract surface, and the operator attestation (where applicable).

---

## 1. IaC module deltas

### 1.1 `iac/modules/app-registration-roles/` — new App Role

**Input addition** (in dev/test/prod composition):
```hcl
role_definitions = {
  # ... existing four roles (Admin, Operator, Reader, Developer) untouched
  "namespace-administrator" = {
    role_id              = "<STABLE_UUID_TBD>"   # generated at planning time; pinned here at task time
    allowed_member_types = ["User", "Group"]
    display_name         = "Namespace Administrator"
    description          = "May onboard, edit, lifecycle-transition, and validate Azure Service Bus namespaces."
    value                = "BusTerminal.NamespaceAdministrator"
  }
}
```

**Module output** (no shape change): the existing `app_role_ids` map gains a new key `namespace-administrator → <UUID>`.

**Operator attestation**: post-IaC-apply, an Entra tenant administrator MUST assign the new role to the appropriate users/groups via Enterprise App assignment per the spec-003 runbook (`specs/003-auth-and-identity/quickstart.md §A.2.3`). The role does NOT take effect until assignments are made.

### 1.2 `iac/modules/graph-permissions/` — new application permission

**Input addition** (in dev/test/prod composition):
```hcl
granted_application_permission_ids = [
  "df021288-bdef-4463-88db-98f22de89214",   # User.Read.All (existing — consented per spec 003)
  "5b567255-7703-4780-807c-7be8301ae99b"    # Group.Read.All (NEW — must be admin-consented post-apply)
]
```

**Module output**: no new outputs. The existing module declares the permission requests; admin consent is procedural.

**Operator attestation**: tenant administrator MUST grant admin consent post-apply via:
```bash
az ad app permission admin-consent --id <BusTerminalApiAppId>
```
This is the same flow used in dev for `User.Read.All` per [[project_admin_consent_pending]] (consented 2026-06-14). The attestation MUST be captured in the deployment runbook entry "Pre-go-live attestations — Graph permissions" alongside the spec 003 entry.

### 1.3 `iac/modules/cosmos-registry-store/` — new container

**Input addition**:
```hcl
containers = [
  # ... existing containers (registry-entities, registry-audit, registry-entities-leases) untouched
  {
    name             = "namespace-validation-runs"
    partition_key    = "/namespaceId"
    autoscale_min_ru = 1000              # lowest band the account allows; matches registry-audit
    autoscale_max_ru = 4000
    default_ttl      = null              # indefinite retention per spec FR-031
  }
]
```

**Module output**: existing `container_names` output (a map of friendly name → resource name) gains `namespace-validation-runs → <full-resource-name>`. Consumed by `BusTerminal.Api`'s `CosmosRegistryOptions` via the existing pattern.

**No new module.** This is a one-entry addition to an existing module's variable.

### 1.4 `iac/platform-bootstrap/main.tf` — pipeline MI RBAC-Admin allowlist extension

**Edit** (lines 231–239, condition v2.0):
```diff
  Microsoft.Authorization/roleAssignments/write requires:
    role_definition_id ∈ {
      7f951dda-4ed3-4680-a7ca-43fe172d538d, # AcrPull
      4633458b-17de-408a-b874-0445c86b69e6, # Key Vault Secrets User
      b86a8fe4-44ce-4948-aee5-eccb2c155cd7, # Key Vault Secrets Officer
      69a216fc-b8fb-44d8-bc22-1f3c2cd27a39, # Azure Service Bus Data Sender
      4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0, # Azure Service Bus Data Receiver
      8ebe5a00-799e-43f5-93ac-243d3dce84a7, # Search Index Data Contributor
-     3913510d-42f4-4e42-8a64-420c390055eb  # Monitoring Metrics Publisher
+     3913510d-42f4-4e42-8a64-420c390055eb, # Monitoring Metrics Publisher
+     acdd72a7-3385-48ef-bd42-f606fba81ae7  # Reader (spec 008 — forward optionality for IaC-driven namespace grants)
    }
```

**Rationale**: enables a future IaC-driven Reader grant pathway without re-litigating the policy. Even though v1 uses the runbook (Complexity Tracking #1), the allowlist extension is non-breaking and preserves forward optionality.

**Test impact**: BT-IAC-004 policy gate (`iac/policies/check-rbac-scope.sh`) must continue to PASS — Reader at namespace scope is permitted; subscription-wide Reader remains forbidden.

### 1.5 `iac/runbooks/grant-namespace-reader.md` — new operator runbook

**New file** (referenced from the wizard step 1 sidebar).

Content outline (full content provided in `quickstart.md §6`):
- Why this is required (link to spec.md FR-014)
- How to find the BusTerminal workload UAMI principalId (`GET /api/namespaces/identity`)
- The `az role assignment create` command template
- How to verify (run `az role assignment list --assignee {principalId} --scope {armId}`)
- Rollback (`az role assignment delete`)

---

## 2. Backend output deltas

### 2.1 New `OnboardedNamespace` document shape (extends `RegistryNamespace`)

Persisted in the existing `registry-entities` container; consumers (spec-006 polymorphic GET, spec-008 GET) deserialize via System.Text.Json. The shape is published in:
- [`onboarded-namespace.schema.json`](./onboarded-namespace.schema.json) — JSON Schema
- [`namespace-onboarding-api.yaml`](./namespace-onboarding-api.yaml) — OpenAPI 3.1 `OnboardedNamespace`

**Backward compatibility**: spec-006 readers continue to deserialize Onboarded documents without modification; the new fields appear as additional properties.

### 2.2 New `ValidationRun` document shape

Persisted in the new `namespace-validation-runs` container. Published in:
- [`validation-run.schema.json`](./validation-run.schema.json)
- [`namespace-onboarding-api.yaml`](./namespace-onboarding-api.yaml) — `ValidationRun`

### 2.3 Extended `AuditEvent` (registry-audit container)

The existing spec-006 `AuditEvent` record gains five new `eventType` values plus a nullable `lifecycleReason` field. Published in:
- [`namespace-audit-event.schema.json`](./namespace-audit-event.schema.json)
- Spec-006 [`audit-event.schema.json`](../../006-service-bus-registry-core/contracts/audit-event.schema.json) — to be updated as a follow-up note to reference the new event types (NOT in this slice's task graph).

### 2.4 OpenAPI document additions

The runtime OpenAPI document served at `GET /openapi/v1.json` (or equivalent) gains the spec-008 routes under `/api/namespaces/*` and `/api/namespaces/_validate` and `/api/namespaces/_picker` and `/api/namespaces/identity`. The authoring source is [`namespace-onboarding-api.yaml`](./namespace-onboarding-api.yaml); a CI assertion verifies the runtime document conforms (same pattern as spec 006).

### 2.5 New `RolePolicies.CanAdministerNamespaces` policy

Registered alongside the existing `CanRead`, `CanMutateDomain`, `CanOperatePlatform`, `CanAdminister`, `CanUseDeveloperTooling` policies. Backed by the new `PlatformRole.NamespaceAdministrator` enum value and the new `BusTerminal.NamespaceAdministrator` claim string.

---

## 3. Frontend output deltas

### 3.1 New routes under `/namespaces`

| Route | Purpose |
|---|---|
| `/namespaces` | Inventory (Server Component list + Client filter/search) |
| `/namespaces/onboard` | 5-step wizard (Client) |
| `/namespaces/{id}` | Details (Server Component) |
| `/namespaces/{id}/edit` | Metadata + ownership edit forms (Client) |
| `/namespaces/{id}/lifecycle` | Lifecycle transition dialog flow (Client) |

### 3.2 New nav entry

Added to the hardcoded `NAV_ENTRIES` array in `web/components/layout/navigation-shell.tsx`:
```ts
{ href: "/namespaces", label: "Namespaces", operationClass: "Read", icon: Layers, matchPrefix: true }
```

Gated by `Read` operation class (= any authenticated tenant user can see the link); the wizard and write actions inside the section are gated separately by `namespace-administrator` role at the API layer.

---

## 4. Cross-spec coordination items (follow-ups, NOT in this slice's task graph)

1. **Spec 003 role-permission matrix contract document**: update `specs/003-auth-and-identity/contracts/role-permission-matrix.md` to add `namespace-administrator` as a fifth role. Tracked as a follow-up PR; does NOT block spec 008 task generation.
2. **Spec 006 audit-event schema**: update `specs/006-service-bus-registry-core/contracts/audit-event.schema.json` (or add a `$defs` reference to the new event types). Tracked as a follow-up PR.
3. **Tech-stack reference** (`speckit-artifacts/tech-stack.md`): add `Azure.ResourceManager.ServiceBus` to the approved backend dependency list. Tracked as a follow-up PR per the project's "spec adds durable rule → update tech-stack.md" convention.

---

## 5. Pre-go-live attestation checklist (additions)

Each spec-008 environment go-live MUST capture the following attestations in the deployment runbook (in addition to the spec-006 FR-037 tenant-population attestation):

- [ ] `Group.Read.All` Graph permission admin-consented for the BusTerminal API app in this environment.
- [ ] At least one Entra principal (user or group) assigned to the `Namespace Administrator` App Role on the BusTerminal API Enterprise App in this environment.
- [ ] Pipeline MI RBAC-Admin condition v2.0 reflects the Reader role GUID (`acdd72a7-3385-48ef-bd42-f606fba81ae7`); BT-IAC-004 gate passing.
- [ ] Operator runbook `iac/runbooks/grant-namespace-reader.md` reviewed and accessible to expected namespace-onboarding operators.
- [ ] The `/api/namespaces/identity` endpoint returns the expected workload UAMI principalId for this environment (smoke check post-deploy).

These attestations MUST appear in the same runbook section as the spec-005 / spec-006 attestations to keep the pre-go-live gate single-page.
