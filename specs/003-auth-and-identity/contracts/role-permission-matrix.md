# Role-Permission Matrix

**Status**: Authoritative · **Slice**: 003-auth-and-identity · **Last reviewed**: 2026-05-19

This document is the binding contract for BusTerminal's role-based authorization. Every protected endpoint in the platform — current and future — declares an **operation class** (the *what*); this matrix declares which **platform roles** (the *who*) may invoke each class. The matrix is mirrored in code at `api/BusTerminal.Api/Authorization/RolePolicies.cs`; if the two disagree, **this document is authoritative** and the code is a defect.

A caller's `effectiveRoles` is the set of `BusTerminal.*` roles resolved from its access token's `roles` claim (see [`data-model.md` § Platform Principal](../data-model.md#1-platform-principal-in-process-value-type-request-scoped)). Authorization for an operation class succeeds if `effectiveRoles ∩ authorizedRoles(operationClass)` is non-empty. A caller with no effective roles is rejected from every role-gated operation class (FR-010).

---

## The Matrix

| Operation Class | Reader | Developer | Operator | Admin |
|---|:---:|:---:|:---:|:---:|
| `Read` | ✅ | ✅ | ✅ | ✅ |
| `MutateDomain` | ❌ | ❌ | ✅ | ✅ |
| `OperatePlatform` | ❌ | ❌ | ✅ | ✅ |
| `Administer` | ❌ | ❌ | ❌ | ✅ |
| `DeveloperTooling` | ❌ | ✅ | ❌ | ✅ |

Equivalent compact form:

| Role | Authorized Operation Classes |
|---|---|
| `BusTerminal.Reader` | Read |
| `BusTerminal.Developer` | Read, DeveloperTooling |
| `BusTerminal.Operator` | Read, MutateDomain, OperatePlatform |
| `BusTerminal.Admin` | Read, MutateDomain, OperatePlatform, Administer, DeveloperTooling |

---

## Operation Class Definitions

Operations are categorized by *intent and blast radius*, not by HTTP verb. A `POST` that triggers an idempotent operational action (e.g., "re-index this view") is `OperatePlatform`, not `MutateDomain`. A `GET` that exposes internal diagnostic state is `DeveloperTooling`, not `Read`.

### `Read`

GET-style queries against domain or platform state visible in normal operational use. Reading a namespace, listing queues, viewing a topology graph, fetching a contract. Audit-log queries are `Administer`, not `Read` (administrative observability is privileged).

**Probe endpoint**: `GET /probe/read`

### `MutateDomain`

Create, update, or delete operations against the messaging-domain entities BusTerminal models — namespaces, queues, topics, subscriptions, rules, contracts, ownership metadata, dependency-graph annotations. The defining property: the call changes durable domain state that another user can observe.

**Probe endpoint**: `POST /probe/mutate-domain`

### `OperatePlatform`

Operational actions that change platform behavior but not domain truth: trigger a discovery run, clear a cache, retry a failed background job, re-emit a telemetry sample. The defining property: the action affects how BusTerminal *behaves* but not what BusTerminal *knows*.

**Probe endpoint**: `POST /probe/operate`

### `Administer`

Platform-wide configuration and privileged actions: tenant-level settings, role-assignment-adjacent operations (note: actual Entra app role assignment is **out-of-band**, performed in the Entra portal — but operations that *depend on* who can assign roles fall here), retention-policy changes, environment-level toggles, audit-log access.

**Probe endpoint**: `POST /probe/administer`

### `DeveloperTooling`

API explorer surfaces, OpenAPI documents, developer-facing diagnostic endpoints not gated as `Read`. The defining property: the operation exists to help a developer integrate with BusTerminal rather than to operate BusTerminal in production. Examples: `/swagger`-style explorer, OpenAPI fetch with extended internal annotations, request-replay endpoints for testing client code.

**Probe endpoint**: `GET /probe/developer`

---

## Implementation Binding

In the backend (`api/BusTerminal.Api/Authorization/RolePolicies.cs`), the matrix is expressed as a single `IServiceCollection` extension:

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("CanRead", p => p.RequireRole(
        "BusTerminal.Reader",
        "BusTerminal.Developer",
        "BusTerminal.Operator",
        "BusTerminal.Admin"))
    .AddPolicy("CanMutateDomain", p => p.RequireRole(
        "BusTerminal.Operator",
        "BusTerminal.Admin"))
    .AddPolicy("CanOperatePlatform", p => p.RequireRole(
        "BusTerminal.Operator",
        "BusTerminal.Admin"))
    .AddPolicy("CanAdminister", p => p.RequireRole(
        "BusTerminal.Admin"))
    .AddPolicy("CanUseDeveloperTooling", p => p.RequireRole(
        "BusTerminal.Developer",
        "BusTerminal.Admin"));
```

Endpoints declare their operation class via `.RequireAuthorization("CanXxx")`. Endpoints **must not** call `.RequireRole(...)` directly — the matrix is the single source of truth and bypassing it via inline role checks is a defect.

---

## Frontend Binding

The frontend exposes role-aware affordances by querying effective roles from `/whoami` and applying the same matrix client-side. The matrix is encoded in `web/lib/auth/role-permission-matrix.ts` and **must** stay in sync with this document. A separate Vitest unit-test asserts that the frontend's matrix encoding matches the contract here.

Frontend affordances apply two patterns from FR-006:

1. **Hidden**: navigation entries and route guards rendered conditionally on whether the caller satisfies the route's operation class.
2. **Disabled with tooltip**: in-page action buttons rendered always but disabled when the caller is unauthorized, with an accessible explanation that names the required role(s).

The frontend's role-aware UI is **never** the security boundary. The backend rejects unauthorized calls regardless of UI state.

---

## Change Control

Changes to the matrix are spec changes, not implementation changes. To change:

1. Update the corresponding spec FR (FR-009b) via a new clarification or follow-up spec.
2. Update this document in the same PR as the spec change.
3. Update `RolePolicies.cs` and `web/lib/auth/role-permission-matrix.ts` accordingly.
4. Re-run the role-probe integration tests; all five probes × four roles × no-role combinations (25 cases) must pass with the new matrix.

The matrix is intentionally conservative: when in doubt, prefer the smaller authorized-role set. Loosening is easier than tightening once consumers depend on a permission.
