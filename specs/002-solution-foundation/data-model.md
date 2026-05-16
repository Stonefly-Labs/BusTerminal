# Data Model: Solution Foundation

**Feature**: 002-solution-foundation
**Date**: 2026-05-16
**Status**: Phase 1 design output

---

## Scope

This is a **platform foundation slice**. It introduces **no domain entities**. Cosmos DB, Azure AI Search, and the Service Bus registry data model all land in later slices and are explicitly out of scope here (see spec § Out of Scope).

What this slice *does* establish are durable **platform constructs** — configuration-shaped, not data-shaped entities — that every later feature depends on. These are documented below at logical-design granularity so that downstream slices have a consistent vocabulary and so that operational concerns (naming, tagging, RBAC scoping, telemetry partitioning) are answered once.

No database schemas, no migrations, no ORM models. The closest analogue here is "the topology of platform resources and the relationships between them" — useful to write down so the planning and tasks phases reason from the same picture.

---

## Platform Constructs

### Environment

Represents a named, isolated deployment target. The slice ships `dev` provisioned end-to-end; `test` and `prod` are scaffolded as folder/parameter-file templates only.

**Logical attributes**:
- `name`: short identifier — `dev`, `test`, `prod`
- `azure_subscription_id`: target subscription (may be the same across environments for `dev`/`test`, separate for `prod` — a deployment-time choice)
- `azure_region`: primary region (default: `East US 2`)
- `resource_group_name`: derived `rg-busterminal-<name>`
- `naming_prefix`: derived `bt-<name>`
- `tags`: a standardized tag set applied to every resource (`environment`, `slice`, `owner`, `cost-center`, `managed-by=opentofu`)

**Lifecycle**:
- Created exactly once via `iac/platform-bootstrap/` (state backend + pipeline identity) followed by `iac/environments/<env>/` `tofu apply`.
- Updated via pipeline-driven `tofu apply` on every relevant merge.
- Destroyed only via explicit operator action; no automated teardown.

**Isolation guarantees** (FR-100):
- Distinct resource group(s).
- Distinct user-assigned managed identities for both workloads and the pipeline.
- Distinct state file partition (`key = "envs/<env>/terraform.tfstate"`).
- Distinct Key Vault, Container Apps Environment, ACR (optional — `dev` may share registry with `test`), Log Analytics Workspace, Application Insights.

**Relationships**:
- 1 Environment → N **Workloads** (initially 2: frontend + backend)
- 1 Environment → 1 **Telemetry Stream**
- 1 Environment → 1+ **Identity Configurations** (workload MIs + Entra ID app registration shared across environments per the standard pattern)

---

### Workload

Represents a single deployable container running on the managed container platform. This slice ships two: `frontend` and `backend`.

**Logical attributes**:
- `name`: short identifier — `frontend` or `backend`
- `environment`: the parent Environment
- `image_reference`: ACR-hosted image tag, e.g. `<acr>.azurecr.io/busterminal/frontend:<git-sha>`
- `managed_identity`: a user-assigned managed identity dedicated to this workload
- `ingress`:
  - `external`: `true` for both workloads in this slice (per spec clarification 2026-05-16 Q2)
  - `target_port`: workload-specific (e.g., 3000 for frontend, 8080 for backend)
  - `transport`: `auto` (HTTP/2 negotiated)
- `scale`:
  - `min_replicas`: 0 (scale-to-zero enabled in `dev`)
  - `max_replicas`: 3 (cost-bounded for `dev`; environment-tunable)
  - `rules`: a single HTTP-concurrency rule (50 concurrent requests per replica triggers scale-out)
- `health_probes`:
  - `liveness` → `GET /healthz/live` (5s timeout, 10s interval)
  - `readiness` → `GET /healthz/ready` (5s timeout, 10s interval)
  - `startup` → `GET /healthz/startup` (5s timeout, 5s interval, 30 failures allowed = 150s startup grace)
- `secrets`: zero or more Key Vault references injected as environment variables via Container Apps secret bindings, fetched using the workload's managed identity
- `env_vars`: non-secret configuration, including `APPLICATIONINSIGHTS_CONNECTION_STRING` (as a Key Vault reference) and W3C-Trace-Context-aware OTel knobs

**Differences between workloads**:
- **Frontend** additionally has the Auth.js secret (signing key) and the Entra ID client secret (for confidential-client OBO flow) as Key Vault references. Frontend has a `Storage Blob Data Reader` role on no resources in this slice but the pattern is ready.
- **Backend** has access to a future Cosmos DB / AI Search managed identity grant (placeholder; no resources yet). Backend has *no* secrets in this slice — it validates tokens using Entra ID's public metadata only.

---

### Identity Configuration

Represents the Entra ID and Azure RBAC plumbing that enables interactive sign-in, API token validation, and service-to-service auth.

**Logical components**:
- **Entra ID Application Registration** (one for the platform):
  - Display name: `BusTerminal Platform (dev)`
  - Redirect URI(s): the deployed frontend URL + `http://localhost:3000/api/auth/callback/microsoft-entra-id` for local dev
  - API exposure: `api://<frontend-client-id>/access_as_user` scope, consentable by users
  - Token version: v2.0 only
  - **No app roles** in this slice (per spec clarification 2026-05-16 Q5; "authenticated vs. unauthenticated only")
- **Workload Managed Identities** (one per workload):
  - `mi-bt-frontend-dev`, `mi-bt-backend-dev` (user-assigned)
  - Both granted `Key Vault Secrets User` on the env's Key Vault
  - Both granted `Monitoring Metrics Publisher` on the env's Application Insights resource
  - Frontend additionally federated to its Entra app registration for client-credentials flow when needed
- **Pipeline Identity** (one per environment):
  - `mi-bt-pipeline-dev` (user-assigned)
  - Federated credential subject: `repo:<org>/BusTerminal:environment:dev`
  - RBAC: `Contributor` on the `dev` resource group; `Storage Blob Data Contributor` on the tfstate storage account; `AcrPush` on the registry; `User Access Administrator` scoped narrowly to the env's resource group (required to assign roles to newly created MIs)

**Lifecycle**:
- App registration is created by an admin one-time (documented in `docs/identity-and-secrets.md`). The bootstrap module *cannot* fully automate this because creating Entra app registrations requires Entra-level permissions that are typically not delegated to a pipeline.
- Workload and pipeline managed identities are created and updated by the environment's OpenTofu apply.
- Federated credentials on the pipeline identity are created by the bootstrap module.

---

### Pipeline Run

Represents a single execution of a CI or CD workflow tied to a commit. Conceptual entity only — GitHub Actions owns the actual persistence.

**Logical attributes**:
- `workflow`: one of `ci`, `cd-dev`, `iac-validate`
- `commit_sha`: the source commit
- `branch_or_pr`: the triggering ref
- `pipeline_identity`: federated managed identity used for Azure access (for CD workflows only)
- `artifacts_produced`:
  - For `ci`: test reports, container image tags
  - For `cd-dev`: deployed revision IDs for each workload, applied OpenTofu plan summary
- `outcome`: `success` | `failure`
- `telemetry_correlation_id`: every deploy emits a single correlation ID logged into Application Insights so an operator can trace "what did this deploy actually change"

**Lifecycle**:
- Created by GitHub on workflow trigger.
- Authenticates to Azure via OIDC federation; the resulting token is short-lived (typically < 1 hour).
- Failed runs leave the previous environment revision in place (Container Apps' default revision-rolling behavior preserves the previous revision until the new one is healthy).

---

### Telemetry Stream

Represents the aggregated logs, traces, metrics, and diagnostics flowing into the centralized destination for a given environment.

**Logical components**:
- **Log Analytics Workspace** (one per environment): receives all Azure resource diagnostic logs (Container Apps system + console logs, Key Vault audit logs, ACR audit logs, Application Insights export, etc.)
- **Application Insights** (one per environment, configured to use the same workspace as its backing store): the OpenTelemetry sink for workload-emitted traces, metrics, and logs
- **Workload diagnostic settings**: every provisioned Azure resource has an opinionated diagnostic-settings configuration that ships all categories + AllMetrics to the workspace
- **Correlation model**: every signal — log line, span, metric, request — carries an operation/trace ID (`traceId` in OTel semantic conventions, `operation_Id` in App Insights) plus optional `parentId` for span hierarchy. This is what makes "search by correlation ID, see frontend + backend" work.

**PII posture** (FR-073):
- Default span attributes exclude user-identifying information.
- The frontend's `whoami` response includes the user's display name and OID, but those values are NOT attached as span attributes — they pass through the response body only.
- A future opt-in spec may add user-tagged telemetry; until then, only correlation IDs propagate.

---

## Relationships Diagram (Logical)

```text
Environment (dev)
├── Workload: frontend ────── Managed Identity (mi-bt-frontend-dev) ──┐
│                                                                     ├── Key Vault Secrets User → Key Vault (kv-bt-dev)
├── Workload: backend  ────── Managed Identity (mi-bt-backend-dev) ───┘
│
├── Identity Configuration
│   ├── Entra App Registration (BusTerminal Platform (dev))
│   ├── Pipeline Identity (mi-bt-pipeline-dev)
│   │   └── Federated Credential ─→ repo:<org>/BusTerminal:environment:dev
│   └── (No app roles — auth-only at this slice)
│
└── Telemetry Stream
    ├── Log Analytics Workspace (law-bt-dev)
    ├── Application Insights (appi-bt-dev) → backed by law-bt-dev
    └── Diagnostic Settings on every provisioned resource → law-bt-dev
```

---

## State Transitions

The only meaningful state machines in this slice are at the workload-revision level (owned by Container Apps) and the OpenTofu state level (owned by the AzureRM backend). Both are external to this design — they are *referenced*, not defined here.

Domain-entity state machines (Namespaces, Queues, Topics, etc.) will be introduced by later slices.
