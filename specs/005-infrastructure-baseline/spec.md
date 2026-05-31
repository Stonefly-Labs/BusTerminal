# Feature Specification: Infrastructure Baseline

**Feature Branch**: `005-infrastructure-baseline`

**Created**: 2026-05-25

**Status**: Draft

**Input**: Source artifact: `speckit-artifacts/005-infrastructure-baseline.md`

**Source Artifact**: [`speckit-artifacts/005-infrastructure-baseline.md`](../../speckit-artifacts/005-infrastructure-baseline.md)

---

## Clarifications

### Session 2026-05-25

- Q: Does this slice physically provision `test` and `prod`, or only deliver the modules + conventions and a fully-deployed `dev`? → A: **Dev only.** Ship the full module set and conventions; deploy them to `dev`; leave `test` and `prod` standup to a later slice. Test/prod environment definitions are provided as templates that an operator can apply later, but this slice does not apply them. The spec's environment-independence requirements (FR-005 through FR-007) are verified by structural review and dry-run of the test/prod templates, not by an applied deployment.
- Q: How does this slice handle the dev resources already deployed by spec `002-solution-foundation` (Container Apps Environment, Container Apps, Key Vault, ACR, Log Analytics, identities)? → A: **Selective retrofit.** Adopt existing `002` resources into the new module structure via OpenTofu `import` (no destructive changes). Add diagnostic settings and additional RBAC to existing resources additively. Defer destructive changes (VNet integration on the existing Container Apps Environment, private-endpointing the existing Key Vault) to a later, narrowly-scoped follow-up spec. Production gets greenfield treatment (no `002` resources exist there yet), so the destructive-retrofit problem is dev-only and short-lived; this slice documents that follow-up as explicit deferred work.
- Q: Does this slice create any Service Bus topics or queues, or only the namespace? → A: **Namespace only.** Provision the namespace, RBAC role assignments for workload identities, a private endpoint where the SKU supports it, and diagnostic settings. Topics, queues, partitioning, dead-letter behavior, and entity naming are owned by the domain specs that need them; this baseline emits the namespace identifiers downstream specs need to create their own entities.
- Q: How are Application Insights ingestion authentication and the connection-string distribution handled, given the contradiction between SC-003/FR-036 (no secret-bearing connection strings) and FR-028 (App Insights connection string routed via Key Vault)? → A: **Hybrid auth; connection string stored in Key Vault and surfaced via Container Apps secret reference.** Backend (server-side .NET in the Container App) uses Microsoft Entra / managed identity for telemetry ingestion (workload identity receives the `Monitoring Metrics Publisher` role on the Application Insights resource). The Application Insights resource MUST keep local-auth ingestion enabled so the browser SDK can ingest. The connection string is stored as a Key Vault secret; each Container App declares a Container Apps `secret` that references the Key Vault secret URI; the workload managed identity holds `Key Vault Secrets User` on the vault; the Container App injects the secret value into the workload environment (and the Next.js frontend forwards it to the browser via `NEXT_PUBLIC_*`-style exposure). The App Insights connection string is therefore NOT a plaintext OpenTofu output but IS materialized as a plaintext env var at runtime by design (the browser ingestion key cannot use managed identity). SC-003 is scoped to data-plane credentials (Cosmos DB keys, Service Bus SAS, Azure AI Search admin keys, Key Vault secret values returned via account key); the App Insights ingestion key is excluded because it grants telemetry-submission only, not data read/write.
- Q: Given the selective-retrofit decision defers VNet integration of the existing dev Container Apps Environment, does this slice still provision the virtual network, private endpoints, and private DNS zones in dev — or skip them entirely in dev and only deploy them in the test/prod templates? → A: **Provision the full network topology in dev now with public access kept enabled on data services via the explicit per-environment toggle.** Dev gets a virtual network, the planned subnets, private DNS zones for every supported service, and a private endpoint per data service (Cosmos DB, Key Vault new private endpoint, Azure AI Search, Service Bus where the SKU supports it). Because the existing dev Container Apps Environment is not VNet-integrated by this slice (per the prior selective-retrofit clarification), those private endpoints are warm but not load-bearing in dev: workloads continue to reach data services over public network access using managed identity until the destructive retrofit follow-up spec flips the CAE into the integration subnet and toggles public access off. Dev therefore exercises the full network module set end-to-end so bugs surface here rather than in test/prod, and the retrofit becomes a minimal change rather than a greenfield rebuild.
- Q: What scope of Azure RBAC role assignments does this slice provision for workload managed identities — only what spec-002's current runtime exercises, or forward-looking grants for the data-plane needs of committed downstream specs (003 auth, 004 core domain model, planned 006+)? → A: **Forward-looking, scoped to committed downstream specs.** The slice provisions the data-plane roles the planned product needs, scoped per-resource per-identity (no subscription-wide grants, no Owner/Contributor on data services, no management roles): backend workload identity receives `Cosmos DB Built-in Data Contributor` on the canonical Cosmos DB account/database scope (for spec 004's persistence), `Key Vault Secrets User` on the environment Key Vault (for spec 003's secret-backed config), `Azure Service Bus Data Sender` and `Azure Service Bus Data Receiver` on the Service Bus namespace (for messaging workflows owned by later specs), `Search Index Data Contributor` on the Azure AI Search service (for search projection), and `Monitoring Metrics Publisher` on the Application Insights resource (per the App Insights clarification above). Frontend workload identity receives only what its Next.js runtime needs: `Key Vault Secrets User` and `Monitoring Metrics Publisher`. Downstream specs do not need to PR new role assignments to ship their first runtime feature; if a future spec needs a role outside this list, it adds the assignment in its own slice with explicit justification.
- Q: Should deployment automation use a single shared managed identity with environment-scoped role assignments, or a dedicated per-environment managed identity? → A: **Per-environment deployment managed identity.** Each environment owns a dedicated user-assigned managed identity (e.g., `mi-busterminal-pipeline-dev`, `-test`, `-prod`), federated to OIDC subjects scoped to that environment only (`repo:Stonefly-Labs/BusTerminal:environment:<env>`), with role assignments scoped to that environment's resource group(s). The existing dev identity (`mi-busterminal-pipeline-dev`) is preserved; new identities are added for test/prod when those slices apply. The "or environment-scoped privileges on a shared deployment identity" alternative in FR-032's original wording is removed. Rationale: a compromised OIDC token exchange leaks one environment's grants only, not all environments; audit answers "who can apply to prod?" with a single dedicated principal; the cost (one MI + one federated credential per env) is trivial.
- Q: What diagnostic log/metric categories does the slice forward to Log Analytics, and what retention is applied? → A: **`allLogs` only (no metrics forwarded to LA); log retention defaults to 30 days in every environment and is exposed as an OpenTofu variable for per-environment override.** Diagnostic settings on every supported platform resource forward `allLogs` (the resource's full log category set, including categories Azure adds in future without spec churn) to the environment's Log Analytics workspace. Platform metrics are NOT forwarded to Log Analytics — they remain in Azure Monitor's native metric store (free, ~93-day rolling window) and are queried directly via the metrics explorer or Application Insights metrics surface. Log Analytics retention defaults to 30 days for `dev`, `test`, and `prod`; the value is exposed as an OpenTofu variable (`log_analytics_retention_days` or equivalent) per environment so operators can dial up or down without modifying the modules. This trade chooses cost discipline (no LA metric ingestion bill, modest retention) over forensic depth; longer retention can be set per-env when an environment's audit/compliance posture demands it.

---

## Overview

BusTerminal is an Azure-first registry/discovery/governance/observability platform for Azure Service Bus topology. Spec `002-solution-foundation` delivered the minimum infrastructure required to host the frontend and backend in Azure (resource group, Container Apps Environment, two Container Apps, Key Vault, ACR, monitoring, workload identity, pipeline identity, federated credentials). It is deliberately partial: there is no Cosmos DB workload binding yet, no Azure AI Search service, no Service Bus namespace, no virtual network, no private endpoints, no test or prod environment, and no formalized policy/security gate around infrastructure changes.

This slice closes that gap. It establishes the **complete infrastructure baseline** required by every later spec: the full set of platform services BusTerminal depends on (Cosmos DB, Azure AI Search, Service Bus, Key Vault, Container Apps, observability, networking, managed identities and RBAC), the conventions that make that baseline repeatable across `dev`, `test`, and `prod`, the security defaults that production must inherit (private networking, least-privilege managed identities, no secrets in source or state), the observability wiring that makes the platform operable, and the validation gates that keep the baseline trustworthy as it evolves.

The deliverable is **infrastructure-as-code only**. Application code, search index schemas, message topologies, and domain logic stay out of scope — they consume the outputs this spec emits (endpoint URLs, resource IDs, managed identity principal IDs) and are owned by the specs that need them.

Treat this slice as a contract surface for every later spec: once the modules, naming, tagging, outputs, and RBAC patterns are established, downstream specs (auth integration, domain model persistence, search projection, messaging workflows) will compose against them. Breaking changes to the module surface area or output shape become expensive as soon as those downstream specs land — so the baseline must be coherent and operationally sound from day one.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Operators can provision the complete BusTerminal platform baseline into a clean Azure subscription (Priority: P1)

A contributor or operator with an empty Azure subscription needs to bring up a full BusTerminal environment: resource group, networking, compute, data services, messaging, secrets, identities, RBAC, and observability — using a documented OpenTofu workflow, without manual portal work and without copy-pasting one-off scripts. When the workflow completes, every platform service BusTerminal depends on exists, is wired together, and emits the outputs later specs need.

**Why this priority**: This is the spec's reason to exist. A baseline that cannot be stood up end-to-end into a clean subscription is not a baseline. Every other story in this slice (managed identity, private networking, observability, multi-environment) is a property *of* this story — so it is P1.

**Independent Test**: Provision a brand-new Azure subscription (or a dedicated empty resource group inside an existing subscription). Run the documented OpenTofu workflow for the target environment. Confirm that on completion: a resource group exists with consistent tags; a virtual network and subnets exist; a Container Apps Environment exists; the frontend and backend Container Apps (or hooks for them) exist; a Cosmos DB account exists; an Azure AI Search service exists; a Key Vault exists; a Service Bus namespace exists; a Log Analytics workspace and Application Insights resource exist; user-assigned managed identities for the workloads and deployment automation exist; the documented outputs are emitted and resolvable. Verifiable without deploying any application image and without writing any spec-006+ code.

**Acceptance Scenarios**:

1. **Given** an empty target resource group in an Azure subscription, **When** the documented deployment workflow is run for an environment, **Then** every baseline resource listed in the In Scope section of the source artifact is provisioned successfully and the workflow exits cleanly.
2. **Given** a successful baseline deployment, **When** an operator queries the OpenTofu outputs, **Then** the outputs include endpoint URLs, resource IDs, managed identity principal IDs, and configuration references required by application deployment workflows — and no output contains a secret value.
3. **Given** a successful baseline deployment, **When** an operator inspects the deployed resources, **Then** every resource carries the documented tag set (environment, owner, cost center, application, managed-by) consistently.
4. **Given** a partially-failed deployment, **When** the workflow is re-run after the underlying cause is fixed, **Then** the deployment converges to the intended state without requiring manual cleanup of in-progress resources.
5. **Given** the workflow uses globally-unique resource names where Azure requires them, **When** the same workflow runs in a second subscription with a different environment prefix, **Then** there are no naming collisions and no manual rename steps are required.

---

### User Story 2 — Application workloads authenticate to every platform service via managed identity, with least-privilege RBAC (Priority: P1)

A backend or frontend Container App needs to read from Cosmos DB, read secrets from Key Vault, query Azure AI Search, send and receive Service Bus messages, and write telemetry to Application Insights — without any connection strings, account keys, or shared access signatures stored in source, state, environment variables, or Key Vault. Every access path resolves through a workload-assigned managed identity holding only the role assignments it needs on only the scopes it needs.

**Why this priority**: Constitution Principle: Managed Identity preferred over secrets. If the baseline ships without identity-based access wired in, every downstream spec inherits a secrets-management problem that is expensive to remove later. This is co-equal P1 with the platform baseline itself.

**Independent Test**: For each first-class platform service (Cosmos DB, Key Vault, Azure AI Search, Service Bus, Application Insights/Log Analytics), confirm that the workload managed identity holds a documented role assignment scoped as narrowly as the service supports (account, namespace, vault, workspace) and that no application secret, key, or connection string for these services is provisioned or persisted by this spec. Confirm that the backend Container App can authenticate to each service using its managed identity from inside the Container Apps Environment, end-to-end, without falling back to keys.

**Acceptance Scenarios**:

1. **Given** the baseline is deployed, **When** the role assignments are listed for the workload managed identity, **Then** each assignment is scoped to a specific resource (not the subscription or the resource group) and uses the least-privileged built-in or custom role appropriate to the workload's needs.
2. **Given** the baseline is deployed, **When** an operator inspects Key Vault, **Then** Key Vault is configured for RBAC authorization (not access policies) and the workload identity holds only the read role(s) needed for the secrets it consumes — not full vault management.
3. **Given** a backend workload running in Container Apps, **When** it calls Cosmos DB, Azure AI Search, or Service Bus from inside the platform, **Then** the call succeeds using the workload managed identity and the corresponding service exposes no key/connection-string usage by that workload.
4. **Given** the OpenTofu outputs, **When** they are searched for secret-like content, **Then** no output contains a Cosmos DB key, Service Bus connection string, Azure AI Search admin key, or any other secret.
5. **Given** the deployment pipeline managed identity, **When** its role assignments are reviewed, **Then** it holds only the privileges required to plan and apply infrastructure changes for its environment — no broader subscription-wide privileges and no application-runtime privileges.

---

### User Story 3 — Production data services are private by default; public exposure is explicit and environment-scoped (Priority: P1)

An operator reviewing the production network topology needs confidence that data services (Cosmos DB, Key Vault, Azure AI Search, Service Bus) are not reachable from the public internet by default, that platform-to-data traffic stays inside the virtual network via private endpoints with private DNS, and that any deviation (e.g., a development environment that permits public access for cost or convenience) is an explicit, environment-scoped toggle — not an implicit default.

**Why this priority**: Constitution Principles: Security by default; minimal public exposure. Once a service is created with public access enabled and traffic begins flowing through that path, removing the public surface becomes a coordinated effort with application teams. Establishing the private-by-default posture in the baseline avoids that drag. P1 because production cannot ship without it and retrofitting it after data flows are established is materially harder.

**Independent Test**: For each data service in the production environment configuration, confirm that public network access is disabled, that a private endpoint is provisioned in the platform virtual network, and that a corresponding private DNS zone is linked to the virtual network so name resolution from inside the VNet returns the private endpoint IP. For the development environment configuration, confirm that public access (if permitted) is gated by an explicit, named toggle that an operator must set — not on by default. Confirm that the public-access posture of every data service is asserted by automated policy/security checks in CI.

**Acceptance Scenarios**:

1. **Given** the production environment is deployed, **When** an operator inspects the public-network-access setting of Cosmos DB, Key Vault, Azure AI Search, and Service Bus, **Then** all are set to disabled and each has a private endpoint in the platform virtual network.
2. **Given** the production environment is deployed, **When** the private DNS zones for the supported services are listed, **Then** each is linked to the platform virtual network and each private endpoint has its DNS record registered.
3. **Given** the development environment configuration, **When** an operator reviews public-access toggles, **Then** public access is enabled in dev only via the explicit per-environment toggle (per the Session 2026-05-25 networking clarification, dev opts into public access until the destructive retrofit lands), the toggle is named clearly enough that its security implication is obvious, and the corresponding private endpoints are still provisioned (warm) so the post-retrofit path is testable.
4. **Given** the platform virtual network, **When** workloads in the Container Apps Environment call data services, **Then** the calls resolve to private endpoint IPs and traffic does not traverse the public internet.
5. **Given** an infrastructure change is proposed, **When** policy/security checks run in CI, **Then** any new resource with public network access enabled or missing diagnostic settings fails the check unless the change carries an explicit, reviewed justification.

---

### User Story 4 — Platform diagnostics and application telemetry route to one observability workspace per environment (Priority: P2)

An operator investigating a backend latency spike or a Service Bus dead-letter event needs to query logs, metrics, traces, and dependency telemetry from a single observability workspace — without stitching together per-resource log streams or following separate retention policies. Container Apps logs, Application Insights telemetry, and diagnostic logs from every supported platform service (Cosmos, KV, AI Search, Service Bus, Container Apps Environment) flow to that workspace using environment-appropriate retention.

**Why this priority**: Constitution Principle: Operability. The baseline is operationally inert without centralized telemetry, but the platform can technically run without it for short periods, so it is P2 rather than P1. Every later operations spec (alerts, dashboards, SLOs) consumes this wiring.

**Independent Test**: For each supported platform resource in the deployed baseline, confirm that diagnostic settings are configured to forward the documented log and metric categories to the environment's Log Analytics workspace. Confirm that Application Insights is workspace-based and binds to the same workspace. Confirm that retention settings differ by environment per the configuration model and are observable in the workspace. Confirm that a synthetic application log line emitted by the backend Container App appears in the workspace via Application Insights.

**Acceptance Scenarios**:

1. **Given** the baseline is deployed, **When** diagnostic settings on the Container Apps Environment, Cosmos DB, Key Vault, Azure AI Search, and Service Bus are inspected, **Then** each forwards `allLogs` to the environment's Log Analytics workspace and does NOT forward metrics to Log Analytics (per the Session 2026-05-25 diagnostics clarification, metrics remain in Azure Monitor's native metric store).
2. **Given** Application Insights is provisioned, **When** its configuration is inspected, **Then** it is workspace-based and bound to the environment's Log Analytics workspace.
3. **Given** the configuration model exposes per-environment Log Analytics retention as an OpenTofu variable (per the Session 2026-05-25 diagnostics clarification, default 30 days everywhere), **When** the deployed retention setting is inspected, **Then** it matches the value declared for that environment, and changing the variable for a single environment does not require modifying shared modules.
4. **Given** the baseline outputs, **When** they are consumed by application deployment, **Then** they include the instrumentation/configuration values needed to bind frontend and backend telemetry to the same workspace (including the Application Insights connection string handle, sourced from Key Vault where appropriate, not emitted as a plaintext output).
5. **Given** a representative workload writes a log entry, **When** the workspace is queried, **Then** the entry is retrievable with environment, resource, and trace-correlation metadata intact.

---

### User Story 5 — `dev`, `test`, and `prod` environments are independently deployable from the same module set without shared mutable state (Priority: P2)

An operator deploying a change to `test` must be confident that the change cannot inadvertently affect `dev` or `prod` — and that promoting the same change to `prod` will produce equivalent topology with environment-appropriate SKUs, retention, capacity, and public-access posture. Each environment has its own resource group, its own naming scope, its own remote state, its own deployment identity (or environment-scoped privileges on a shared identity), and its own configuration values; the OpenTofu module set is shared.

**Why this priority**: P2 because the single-environment baseline (User Story 1) is independently valuable and the rest of the project can progress on `dev` while `test` and `prod` are stood up. But establishing the multi-environment shape in this slice prevents `dev`-only assumptions from leaking into modules and becoming retrofits later.

**Independent Test**: With the module set in place, confirm that an environment definition for `test` (and `prod`) can be created from the documented template by setting only the per-environment configuration inputs (region, name prefix, SKUs, retention, public-access toggles, principal IDs). Confirm that `tofu plan` for a non-existent `test` environment produces a plan that creates the same resource shape as `dev` with environment-appropriate values, against its own remote state, without referencing `dev` state. Confirm that an apply against `test` cannot read or mutate `dev` or `prod` state because state backends are environment-scoped.

**Acceptance Scenarios**:

1. **Given** the module set is in place, **When** a `test` (or `prod`) environment definition is created from the documented template, **Then** only per-environment configuration values must be supplied — module references, output names, and tag conventions are inherited from the shared module set.
2. **Given** each environment has its own remote state backend, **When** a deployment runs against one environment, **Then** it cannot read or write the state of another environment.
3. **Given** the per-environment configuration model, **When** `prod` is compared to `dev`, **Then** SKUs, retention, capacity, and public-access posture differ as configured, but topology (which services exist, which identities exist, which role assignments exist) is equivalent.
4. **Given** a deployment identity, **When** its scope is reviewed, **Then** it can only plan and apply against its target environment's resources — not against other environments' resources.
5. **Given** an operator runs `tofu plan` for a non-existent `prod` environment, **When** the plan output is inspected, **Then** it shows the full create-from-empty plan for `prod` without referencing or depending on `dev` or `test` state.

---

### User Story 6 — Contributors can validate infrastructure changes locally and in CI before any apply (Priority: P3)

A contributor modifying an OpenTofu module or environment configuration runs `tofu fmt`, `tofu validate`, and `tofu plan` locally, and pushes the change. CI re-runs the same checks plus static security scanning and policy checks (no broadly-permissive RBAC, no public-by-default data services, no missing diagnostic settings, no missing tags, no inline secrets) and blocks the change unless all gates pass. Destructive changes (resource replacements affecting stateful services) are surfaced in the plan and require explicit review before apply.

**Why this priority**: P3 because infrastructure changes will happen even without a CI gate (humans can run `tofu plan` and review by eye), but the gate is what keeps the constitution-aligned defaults from drifting. Worth shipping in this slice so that downstream specs inherit the discipline.

**Independent Test**: With the validation tooling in place, confirm that a deliberately-malformed module fails `tofu fmt` and `tofu validate` locally and in CI. Confirm that a deliberately-misconfigured resource (e.g., a Key Vault with `public_network_access_enabled = true` in `prod`, a Cosmos DB without diagnostic settings, a role assignment scoped to the subscription) is flagged by the policy/security gate in CI. Confirm that a plan containing a destructive change to a stateful resource is surfaced visibly in the CI plan summary.

**Acceptance Scenarios**:

1. **Given** a contributor runs the documented local validation commands, **When** the OpenTofu code is well-formed and policy-compliant, **Then** all commands succeed; **when** it is malformed or non-compliant, the failing command exits non-zero with an actionable message.
2. **Given** a pull request modifying infrastructure code, **When** CI runs, **Then** it executes formatting, validation, planning, security scanning, and policy checks for each affected environment, and posts the plan summary (without secret values) to the pull request.
3. **Given** a plan containing a destructive change to a stateful resource (Cosmos DB, Key Vault, state storage), **When** CI surfaces the plan, **Then** the destructive change is highlighted and a manual approval is required before apply.
4. **Given** a change introduces a public-network-access toggle on a production data service or removes a diagnostic setting, **When** policy checks run, **Then** the change is blocked unless an explicit, reviewed justification accompanies it.
5. **Given** CI logs are reviewed, **When** searched for secret-like values, **Then** no secret values appear (no Cosmos keys, no Service Bus connection strings, no Key Vault contents).

---

### User Story 7 — Re-applies are safe; stateful resources are not destroyed without explicit intent (Priority: P3)

An operator running an apply against a healthy environment must be confident that the apply will not destroy or recreate stateful resources (Cosmos DB account, Key Vault, state storage account, Container Registry) as a side-effect of an unrelated module change. Resource lifecycle protections, soft-delete / purge-protection settings, and module separation between stateful and stateless concerns make destructive replacements an explicit choice — not an accident.

**Why this priority**: P3 because the single-environment build (US1) can ship without this hardening, but every operator who runs a second apply will benefit, and the cost of skipping it shows up as data-loss incidents. Worth shipping in this slice.

**Independent Test**: With the baseline deployed, simulate a series of representative changes (rename a non-stateful resource, change a Container App's image, change a Container App's environment variable, change a tag value on the resource group) and confirm `tofu plan` shows no destroy actions against Cosmos DB, Key Vault, the Log Analytics workspace, Application Insights, the state storage account, or any other resource carrying data. Confirm that Key Vault in `prod` has purge-protection enabled. Confirm that the state storage account is configured with deletion protection where supported.

**Acceptance Scenarios**:

1. **Given** the baseline is deployed, **When** an unrelated infrastructure change is planned, **Then** `tofu plan` shows zero destroy actions against Cosmos DB, Key Vault, Log Analytics, Application Insights, the state storage account, or any resource holding data.
2. **Given** the production environment, **When** Key Vault settings are inspected, **Then** purge protection is enabled and soft delete is enabled.
3. **Given** the state storage account, **When** its protection settings are inspected, **Then** accidental-deletion protection is enabled to the extent supported by the storage account SKU.
4. **Given** a change that *intentionally* requires replacing a stateful resource, **When** the plan is reviewed, **Then** the replacement is explicit in the plan output and requires the destructive-change manual approval defined in User Story 6.

---

### Edge Cases

- A deployment is interrupted mid-apply (CI timeout, network failure, credential expiry) — the next apply must converge to the intended state without manual cleanup of partially-created resources.
- A resource that requires global uniqueness (storage account name, Key Vault name, Cosmos account name) collides with a pre-existing resource in the same or another subscription — the naming convention must prevent collision through environment- and instance-aware prefixes/suffixes; the failure mode must be a clear plan-time error, not a partial deployment.
- A spec-002 resource (Container Apps Environment, Key Vault, ACR, monitoring) was created before this baseline was formalized — adopting it into the new module structure must not destroy or recreate the resource; the baseline must support import or reference of the existing resource.
- The operator's account does not have sufficient subscription-level privileges to create role assignments — the failure must be at plan time with a clear message identifying the missing privilege, not at apply time after partial resources are created.
- A future spec needs a new role assignment or a new diagnostic setting on a baseline resource — the module set must accept an additive extension without requiring a fork or re-architecture.
- A data-service public-access posture is changed (e.g., dev permits public, prod blocks it) — workloads inside the Container Apps Environment must continue to function in both modes because access is routed through identity, not via specific endpoints.
- Application Insights or Log Analytics is unavailable at deployment time — the baseline deployment must still complete; downstream applications must degrade gracefully without diagnostic transport.
- An OpenTofu provider or module version drifts between contributors — version pinning and lockfile commitment must make the plan deterministic across machines.

---

## Requirements *(mandatory)*

### Functional Requirements

#### Infrastructure-as-Code Tooling and Layout

- **FR-001**: The solution MUST define all Azure infrastructure for the BusTerminal baseline using OpenTofu. No Bicep, ARM, or Pulumi may be introduced for this baseline.
- **FR-002**: The solution MUST organize OpenTofu code into reusable modules grouped by capability boundary (naming, networking, container-apps, cosmos-db, ai-search, key-vault, service-bus, observability, managed-identity, role-assignments, and supporting modules as needed), with per-environment composition layers that consume those modules.
- **FR-003**: Modules MUST expose clear, documented inputs and outputs; MUST avoid hardcoded subscription IDs, tenant IDs, resource group names, or region names; MUST support tagging; MUST expose every resource ID, principal ID, or endpoint that any dependent module or environment composition needs; and MUST avoid circular dependencies.
- **FR-004**: Stateful services MUST be separated from stateless compute/runtime modules where practical so that lifecycle and replacement semantics differ cleanly.

#### Environments

- **FR-005**: The solution MUST support environment-specific configuration for at least `dev`, `test`, and `prod`, with each environment producing an independent set of resources in its own resource group and its own remote state. Per the Session 2026-05-25 clarification, this slice physically provisions `dev` only; `test` and `prod` are delivered as environment composition templates that an operator can apply in a later slice without further code changes.
- **FR-006**: Each environment definition MUST be reproducible from the documented template by supplying only the per-environment configuration values (name, region, name prefix/suffix, tags, SKUs, network CIDRs, public-access toggles, private-endpoint toggles, retention values, container image placeholders, principal IDs). The `test` and `prod` templates MUST be verifiable via `tofu validate` and structural review without requiring an apply.
- **FR-007**: Environments MUST be independently deployable; an apply against one environment MUST NOT be able to read or mutate another environment's state or resources.

#### Container Apps and Compute

- **FR-008**: The solution MUST provision an Azure Container Apps Environment suitable for hosting the frontend and backend application workloads.
- **FR-009**: The solution MUST provision the frontend Container App and the backend Container App, OR MUST expose stable module hooks that application deployment workflows can use to create them — whichever model is chosen MUST satisfy the existing CI/CD deployment flow established by spec `002-solution-foundation`.
- **FR-010**: Container App ingress configuration MUST be environment-aware: frontend external ingress MAY be enabled per environment; backend ingress MUST default to internal in production unless external API exposure is explicitly required.
- **FR-011**: Container App environment-variable configuration MUST support sourcing non-secret values from OpenTofu outputs and secret values via Key Vault references — never via plaintext secret variables.
- **FR-012**: Container App telemetry MUST be wired to the environment's observability resources.

#### Data Services

- **FR-013**: The solution MUST provision a Cosmos DB account for metadata persistence, with capacity, consistency, and backup configuration appropriate to each environment.
- **FR-014**: Cosmos DB MUST support identity-based access by application workloads via managed identity; the workload MUST NOT receive a Cosmos DB account key from this baseline.
- **FR-015**: The solution MUST provision an Azure AI Search service with SKU appropriate to each environment.
- **FR-016**: Azure AI Search MUST support managed-identity access patterns where available; admin keys MUST NOT be exposed as workload-consumable outputs.
- **FR-017**: Cosmos DB and Azure AI Search MUST support private-endpoint configuration via per-environment toggles; production MUST default to private endpoints with public network access disabled.

#### Secrets

- **FR-018**: The solution MUST provision an Azure Key Vault per environment, configured for RBAC authorization (not access policies) unless a documented technical constraint requires otherwise.
- **FR-019**: Key Vault in production MUST enable purge protection; all environments MUST enable soft delete.
- **FR-020**: Key Vault MUST support private-endpoint configuration via a per-environment toggle; production MUST default to private endpoints with public network access disabled.
- **FR-021**: The solution MUST NOT provision long-lived application secrets unless explicitly required by a later spec; secret materialization remains the responsibility of the spec that introduces the dependency.

#### Messaging

- **FR-022**: The solution MUST provision a Service Bus namespace per environment. Per the Session 2026-05-25 clarification, this slice does NOT create any topics, queues, subscriptions, rules, or other entities inside the namespace; those are owned by the domain specs that need them. The namespace MUST emit the resource ID, FQDN, and any identifiers downstream specs need to create their own entities.
- **FR-023**: Service Bus MUST be accessed by application workloads via managed identity and Azure RBAC; SAS-based connection strings MUST NOT be exposed as workload-consumable outputs.
- **FR-024**: Service Bus MUST support private-endpoint configuration via a per-environment toggle where the chosen SKU supports it; production MUST default to private endpoints with public network access disabled when supported by the SKU.

#### Observability

- **FR-025**: The solution MUST provision a Log Analytics workspace per environment. Per the Session 2026-05-25 diagnostics clarification, log retention MUST default to 30 days across `dev`, `test`, and `prod`, and MUST be exposed as a per-environment OpenTofu variable so operators can override the default without modifying modules. The retention variable MUST have a documented input name and a documented permissible range.
- **FR-026**: The solution MUST provision a workspace-based Application Insights resource per environment, bound to that environment's Log Analytics workspace.
- **FR-027**: The solution MUST configure diagnostic settings on every supported platform resource it provisions (Container Apps Environment, Cosmos DB, Key Vault, Azure AI Search, Service Bus, and any other supported services) to forward `allLogs` (the resource's full log category set, including categories Azure adds in future) to the environment's Log Analytics workspace. Per the Session 2026-05-25 diagnostics clarification, platform metrics MUST NOT be forwarded to Log Analytics via diagnostic settings; metrics remain in Azure Monitor's native metric store and are queried via the metrics explorer or Application Insights metrics surface.
- **FR-028**: Observability outputs MUST include the resource-level identifiers needed by application configuration (Application Insights resource ID, Log Analytics workspace ID, Application Insights resource name) without emitting the App Insights connection string as a plaintext OpenTofu output. Per the Session 2026-05-25 clarification on observability auth, the App Insights connection string MUST be stored as a Key Vault secret and surfaced to each Container App via a Container Apps secret reference resolved by the workload managed identity (`Key Vault Secrets User` on the vault). Backend telemetry MUST use Microsoft Entra ingestion via managed identity; the workload identity MUST receive the `Monitoring Metrics Publisher` role on the Application Insights resource. The App Insights resource MUST retain local-auth ingestion enabled so the browser SDK can submit telemetry using the connection string's ingestion key.

#### Networking

- **FR-029**: The solution MUST provision a virtual network and subnets sized for the Container Apps Environment integration subnet and the private-endpoint subnet, per environment — including in dev, even though the existing dev Container Apps Environment is not VNet-integrated by this slice (per the Session 2026-05-25 selective-retrofit clarification). The integration subnet MUST be sized and delegated correctly for a future Container Apps Environment VNet integration; the private-endpoint subnet MUST be sized to accommodate every data-service private endpoint this baseline provisions.
- **FR-030**: The solution MUST provision the private DNS zones required by every private endpoint it supports, and MUST link each zone to the platform virtual network so that name resolution from inside the VNet returns the private endpoint IP. Private DNS zones MUST be provisioned in every environment that has a virtual network (including dev), so that the post-retrofit private resolution path is exercised end-to-end as soon as the CAE is VNet-integrated.
- **FR-031**: Public-access toggles for data services MUST be explicit, named, and environment-scoped; the default MUST be private (public disabled). Dev MUST opt into public access via the explicit per-environment toggle until the destructive-retrofit follow-up spec lands; that toggle being on does NOT mean private endpoints are skipped — per the Session 2026-05-25 networking clarification, private endpoints are still provisioned in dev (warm but not load-bearing) so the retrofit is a minimal change.

#### Identity and RBAC

- **FR-032**: The solution MUST provision user-assigned managed identities for the frontend workload, the backend workload, and the deployment automation, per environment. Per the Session 2026-05-25 deployment identity clarification, each environment MUST own a dedicated deployment managed identity (no shared cross-environment identity is permitted); each deployment identity MUST be federated only to OIDC subjects scoped to its target environment.
- **FR-033**: Workload managed identities MUST receive role assignments scoped as narrowly as the target service supports (resource-level where possible) and using the least-privileged built-in or custom role appropriate to the workload's needs, for Cosmos DB, Key Vault, Azure AI Search, Service Bus, and observability access. Per the Session 2026-05-25 RBAC scope clarification, this slice grants the forward-looking set required by committed downstream specs, scoped per-resource per-identity:
  - **Backend workload identity** MUST receive: `Cosmos DB Built-in Data Contributor` scoped to the canonical Cosmos DB account/database; `Key Vault Secrets User` scoped to the environment Key Vault; `Azure Service Bus Data Sender` and `Azure Service Bus Data Receiver` scoped to the Service Bus namespace; `Search Index Data Contributor` scoped to the Azure AI Search service; `Monitoring Metrics Publisher` scoped to the Application Insights resource.
  - **Frontend workload identity** MUST receive: `Key Vault Secrets User` scoped to the environment Key Vault; `Monitoring Metrics Publisher` scoped to the Application Insights resource.
  - Subscription-wide grants, Owner/Contributor on data services, and management-plane roles (Cosmos DB Account Contributor, Service Bus Namespace Owner, Search Service Contributor, Key Vault Administrator) MUST NOT be granted to workload identities by this slice.
  - Any role outside this set required by a future spec MUST be added by that spec with explicit justification.
- **FR-034**: Deployment automation identities MUST receive only the privileges required to plan and apply infrastructure changes for their target environment; subscription-wide or cross-environment privileges are prohibited unless an explicit, documented exception applies.

#### Outputs

- **FR-035**: The solution MUST emit OpenTofu outputs required by later specs, including endpoint URLs, resource IDs, managed identity principal IDs, configuration handles, and observability identifiers.
- **FR-036**: OpenTofu outputs MUST NOT contain secret values (account keys, SAS tokens, admin keys, connection strings containing secrets, Key Vault secret values). Sensitive values MUST be marked sensitive and routed via Key Vault.

#### Tagging

- **FR-037**: The solution MUST apply a consistent tag set to every resource that supports tagging (environment, owner, cost center, application, managed-by, and any project-mandated additional tags), with values supplied per environment.

#### State Management

- **FR-038**: OpenTofu state for shared environments MUST be remote; per-environment state backends MUST be scoped to that environment.
- **FR-039**: State storage MUST be protected from accidental deletion where the backend supports it.
- **FR-040**: State access MUST be limited to deployment identities and authorized maintainers; no broader principal MUST have read or write access to state.

#### Secrets Hygiene

- **FR-041**: Secrets MUST NOT be stored in OpenTofu variables, state outputs, repository files, or CI/CD logs at any point in the change lifecycle.
- **FR-042**: Sensitive variables MUST be marked sensitive so they are not echoed in plan or apply output.

#### Validation and CI/CD

- **FR-043**: The solution MUST document and support local validation commands (`tofu fmt`, `tofu validate`, per-environment `tofu plan`) that contributors can run before pushing changes.
- **FR-044**: Infrastructure changes MUST be gated in CI by formatting check, validation, per-environment plan, static security scanning of OpenTofu configuration, and policy checks for public exposure, missing diagnostics, missing tags, and excessive RBAC.
- **FR-045**: Destructive changes (resource replacements affecting stateful services) MUST be surfaced in the CI plan summary and MUST require explicit reviewer approval before apply.

#### Documentation and Hardening

- **FR-046**: Production hardening switches (private networking, purge protection, deletion protection, public-access disable, retention upgrades) MUST be documented separately from local/development conveniences so the production posture is auditable.
- **FR-047**: Diagnostic logging MUST NOT intentionally capture sensitive payloads; documented categories MUST exclude content that would constitute PII or secret leakage.

### Non-Functional Requirements

- **NFR-001 (Security)**: Infrastructure MUST default to least privilege, managed identity, encrypted services in transit and at rest using Azure-managed keys at minimum, and minimal public network exposure. RBAC assignments MUST be reviewable.
- **NFR-002 (Reliability)**: Stateful services MUST use SKUs and configuration patterns appropriate to the environment tier (e.g., higher availability and backup defaults in production than in development).
- **NFR-003 (Cost awareness)**: Development environments MUST support lower-cost SKUs (e.g., consumption-tier or basic SKUs where viable) while preserving production-compatible topology so that promotion to production does not require resource-shape changes.
- **NFR-004 (Operability)**: Operators MUST be able to inspect logs, metrics, traces, dependency failures, and platform diagnostics from a single observability workspace per environment.
- **NFR-005 (Portability)**: The module set MUST be reusable across Azure subscriptions and regions with environment-specific parameterization; no module may hardcode a subscription, tenant, region, or resource group.
- **NFR-006 (Maintainability)**: Modules MUST be organized by capability boundary and MUST avoid excessive coupling between unrelated platform components; a change to one module (e.g., Service Bus) MUST NOT cascade into unrelated modules (e.g., Cosmos DB) absent a deliberate interface change.

### Key Entities

- **Environment**: A named deployment target (`dev`, `test`, `prod`) with its own configuration, remote state backend, deployment identity scope, naming prefix/suffix, and resource set. Adding a new environment is purely a configuration act.
- **Resource Group**: The Azure container for an environment's platform resources, tagged consistently with environment identification.
- **Container Apps Environment**: The hosting boundary for the frontend and backend Container Apps, integrated with the platform virtual network.
- **Frontend Container App**: The web application runtime; per-environment ingress posture (public or private).
- **Backend Container App**: The API runtime; defaults to internal ingress in production unless explicitly opened.
- **Cosmos DB Account**: The metadata persistence store for the registry; identity-accessed; private-endpoint-capable.
- **Azure AI Search Service**: The search/discovery service for registry projections; identity-accessed where supported; private-endpoint-capable.
- **Key Vault**: The secrets store; RBAC-authorized; soft-delete enabled; purge-protected in production; private-endpoint-capable.
- **Service Bus Namespace**: The internal messaging namespace; identity-accessed; private-endpoint-capable on supporting SKUs.
- **Workload Managed Identity**: User-assigned identity attached to a Container App for accessing platform services; one per workload role, per environment.
- **Deployment Managed Identity**: User-assigned identity used by CI/CD to plan and apply infrastructure changes for a single target environment. Per the Session 2026-05-25 deployment identity clarification, exactly one deployment managed identity exists per environment, federated only to OIDC subjects scoped to that environment.
- **Virtual Network**: The platform network boundary containing the Container Apps integration subnet and the private-endpoint subnet, per environment.
- **Private Endpoint**: A private connectivity point for a supported Azure PaaS service into the platform virtual network.
- **Private DNS Zone**: A DNS zone (per private-endpoint service type) linked to the platform virtual network so name resolution from inside the VNet returns the private endpoint IP.
- **Log Analytics Workspace**: The central telemetry destination per environment; receives platform diagnostics and underpins Application Insights.
- **Application Insights Resource**: Workspace-based; receives application-level telemetry from the frontend and backend.
- **Diagnostic Setting**: The forwarding configuration on a platform resource that routes its log and metric categories to the environment's Log Analytics workspace.
- **Role Assignment**: A scoped Azure RBAC grant from a principal (workload or deployment identity) to a resource (or its narrowest enclosing scope) using a least-privileged role.
- **Tag Set**: A consistent collection of metadata applied to every taggable resource, parameterized per environment.
- **Remote State Backend**: A per-environment state storage location, protected from accidental deletion, with access restricted to deployment identities and authorized maintainers.
- **Configuration Profile**: The per-environment input set (region, name prefix/suffix, tags, SKUs, CIDRs, public-access toggles, private-endpoint toggles, retention, image placeholders, principal IDs) that parameterizes the shared module set.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A contributor can take the documented OpenTofu workflow and bring up a complete BusTerminal environment in an empty Azure resource group in under 60 minutes of unattended runtime, using only documented inputs.
- **SC-002**: 100% of platform services that BusTerminal application workloads depend on (Cosmos DB, Key Vault, Azure AI Search, Service Bus, Application Insights/Log Analytics, Container Apps Environment) are provisioned and connected through documented outputs after a successful baseline apply, with no manual portal steps required.
- **SC-003**: 100% of workload-to-data-plane access paths use managed identity; 0% use Cosmos DB account keys, Service Bus SAS tokens, Azure AI Search admin keys, Key Vault secret values returned via account key, or any other data-plane credential after the baseline is deployed. The Application Insights ingestion key carried in the App Insights connection string is excluded from this criterion because it is a telemetry-submission identifier (write-only, no data read/write) — its handling is governed by FR-028 (stored in Key Vault, surfaced to Container Apps via secret reference).
- **SC-004**: In the production environment, 100% of supported data services (Cosmos DB, Key Vault, Azure AI Search, Service Bus) have public network access disabled and a private endpoint provisioned, with the corresponding private DNS zone linked to the platform virtual network.
- **SC-005**: 100% of OpenTofu outputs emitted by the baseline pass a secrets-content scan (no Cosmos keys, no Service Bus connection strings, no admin keys, no Key Vault secret values).
- **SC-006**: 100% of supported platform resources have diagnostic settings forwarding `allLogs` to the environment's Log Analytics workspace; 0% forward metrics to Log Analytics (per the Session 2026-05-25 diagnostics clarification).
- **SC-007**: A second environment can be added to the project by an operator in under 30 minutes of configuration work, supplying only the per-environment configuration profile and producing an independent resource set in its own state.
- **SC-008**: A pull request modifying infrastructure code passes all CI validation gates (format, validate, plan, security scan, policy checks) before merge in 100% of accepted changes; CI blocks 100% of changes that introduce a missing diagnostic, a public-by-default production data service, or an excessively-scoped role assignment without an explicit reviewed justification.
- **SC-009**: 0 destructive changes to stateful resources (Cosmos DB, Key Vault, Log Analytics, Application Insights, state storage) occur as a side effect of unrelated module changes during normal apply runs.
- **SC-010**: Tag coverage on taggable resources in a deployed environment is 100% for the documented mandatory tag set (environment, owner, cost center, application, managed-by).
- **SC-011**: 100% of deployments use a deployment identity whose privilege scope is bounded to its target environment, verifiable by inspection of the identity's role assignments.

---

## Out of Scope

The following are deliberately excluded from this slice and MUST NOT drive its design:

- Application business logic, domain implementation, and runtime behavior.
- Registry domain model design beyond the infrastructure needed to host it (the domain model is owned by spec `004-core-domain-model`).
- Azure AI Search index schema, analyzers, scoring profiles, or projection logic (only the search service resource and its access wiring are in scope).
- Frontend design system implementation, theming, or component scaffolding (owned by spec `001-brand-system-and-design-foundation`).
- End-user authentication and authorization flows (owned by spec `003-auth-and-identity`); only the infrastructure dependencies (Key Vault, Container Apps managed identity) are in scope here.
- SaaS multi-tenant hosting architecture, tenant isolation, or per-tenant resource partitioning (BusTerminal is single-tenant per the constitution).
- Kubernetes or AKS hosting; the platform targets Azure Container Apps only.
- Bicep, ARM template, or Pulumi implementations (OpenTofu only).
- Image build pipelines, container registry retention/scanning policy beyond the existing baseline established by spec `002-solution-foundation`.
- Service Bus messaging topology design (queue/topic naming, partitioning strategy, dead-letter policy, message contracts). Per the Session 2026-05-25 clarification, this slice provisions the Service Bus namespace only; topics, queues, and subscriptions are owned by the domain specs that need them.
- Destructive retrofits of the live dev `002` resources (VNet integration on the existing Container Apps Environment, private-endpointing the existing Key Vault). Per the Session 2026-05-25 clarification, these are deferred to a later follow-up spec to avoid disturbing the live dev URL and Entra app-registration redirect URIs.
- Physical provisioning of `test` and `prod` environments. Per the Session 2026-05-25 clarification, this slice delivers the environment composition templates and verifies them via structural review and `tofu validate`; an apply against `test` or `prod` is a later slice.
- Alerts, dashboards, SLOs, or runbook automation; only the underlying observability resources and diagnostic-forwarding wiring are in scope.

---

## Assumptions

- BusTerminal is deployed as a single-tenant open-source solution into an organization-owned Azure tenant.
- Application runtime is .NET 10 for backend services and Next.js 16.x for frontend services; Container Apps is the hosting platform; OpenTofu is the only IaC tool.
- Private networking is a production expectation; development environments may permit selected public access for cost or simplicity when explicitly configured via a named per-environment toggle.
- Spec `002-solution-foundation` has already deployed a partial dev environment (resource group, Container Apps Environment, frontend/backend Container Apps, Key Vault, ACR, monitoring/Log Analytics, workload identity, pipeline identity, federated credentials, remote state backend in `rg-busterminal-tfstate`). Per the Session 2026-05-25 clarification, this slice performs a **selective retrofit** of those resources: they are adopted into the new module structure via OpenTofu `import` (no destructive changes), and diagnostic settings + additional RBAC are added additively. Destructive changes that would disturb the live dev URL (notably VNet integration on the existing Container Apps Environment, private-endpointing the existing Key Vault) are explicitly deferred to a later follow-up spec. Production receives greenfield treatment in a later slice and is exempt from this deferral.
- The remote state backend (`rg-busterminal-tfstate` / `btstatech0001`) is treated as already-bootstrapped and external to this slice; per-environment state keys (e.g., `envs/dev/terraform.tfstate`, `envs/test/...`, `envs/prod/...`) live inside that backend.
- Azure Container Registry is treated as already provisioned (`acrbtdevchdev01`) by spec `002-solution-foundation`; this slice does not re-provision a registry for the dev environment but may need to provision registries for `test` and `prod` (subject to Q1 below). Image publishing flows remain owned by the application CI/CD pipelines established in spec `002-solution-foundation`.
- The Cosmos DB account provisioned by spec `004-core-domain-model` (canonical store) is integrated as part of this baseline; if a Cosmos resource already exists in dev, it is adopted into the formalized module structure via import.
- Identity-based access is preferred over secrets wherever Azure supports it; SKUs and features that do not support managed identity are reviewed against alternatives before being adopted.
- Per the Session 2026-05-25 clarification, this slice physically provisions `dev` only; `test` and `prod` are delivered as environment composition templates. The spec's requirements apply equally to all three environments regardless of which are actually stood up in this slice.
- The `infra/opentofu/` directory layout proposed in the source artifact and the existing `iac/` layout used by spec `002-solution-foundation` are treated as a single concern: the implementation will converge on one canonical location, but the spec does not mandate which (the plan phase resolves this).

---

## Dependencies

- **`001-brand-system-and-design-foundation`** — frontend design assumptions only; not required for infrastructure provisioning.
- **`002-solution-foundation`** — application shell, CI/CD conventions, health checks, runtime structure, and the partial dev environment that this baseline extends.
- **`003-auth-and-identity`** — identity conventions and authorization boundaries; managed identity assignments in this baseline must align with the workload/principal model defined there.
- **`004-core-domain-model`** — Cosmos DB usage shape and persistence contract; the Cosmos DB resource provisioned by this baseline must satisfy 004's storage requirements.

---

