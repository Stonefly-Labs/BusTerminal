# Feature Specification: Solution Foundation

**Feature Branch**: `feature/002-solution-foundation`

**Created**: 2026-05-16

**Status**: Draft

**Input**: Source artifact: `speckit-artifacts/002-solution-foundation.md`

---

## Overview

This feature establishes the runnable, deployable application foundation for BusTerminal. It is a pure platform/infrastructure slice: no domain functionality, no Service Bus registry features, no AI capabilities. It produces a working frontend shell, a working backend shell, the Azure hosting environment they run in, the pipelines that deploy them, and the operational guardrails (identity, secrets, logging, telemetry, health, local dev) that every later feature will inherit.

In short: when this slice is complete, **every later feature can begin its work assuming the platform is already there**.

---

## Clarifications

### Session 2026-05-16

- Q: Which Azure environments are provisioned end-to-end in this slice? → A: `dev` only. `test` and `prod` are scaffolded (folder structure + parameter file templates) but their resources are not provisioned in this slice. Adding them later must be a configuration change, not a redesign.
- Q: What is the backend API's ingress posture for the `dev` environment? → A: External ingress on the public internet; every request MUST present a valid Microsoft Entra ID bearer token (no anonymous access). Internal callers (frontend) and external callers (future API consumers) share the same authenticated public surface.
- Q: What does the deployed frontend actually render for an authenticated user in this slice? → A: A navigation shell (header with logo, theme toggle, user menu/sign-out; sidebar/nav placeholder) plus one authenticated "platform status" page that calls the backend's `whoami` endpoint and surfaces correlation IDs. The shell consumes design tokens and primitives from slice 001 (no new design work).
- Q: How is OpenTofu remote state bootstrapped? → A: A dedicated one-time `platform-bootstrap` OpenTofu module provisions the state storage account, container, state locking, and the GitHub Actions federated identity. All shared/CI usage (every environment) uses that remote state. Local developer workflows use local state (no remote-state dependency on a dev's machine). Documentation MUST cover BOTH (a) how to run the one-time bootstrap module and (b) a step-by-step manual procedure (e.g., via `az` CLI / portal) that produces an equivalent backend for developers who would rather not run the module.
- Q: What is the authorization scope for this slice? → A: Authenticated-vs-unauthenticated only. Any signed-in Microsoft Entra ID user may call any protected endpoint. No app roles, no group claims, no role enforcement logic in this slice. Role-based access control ships in a later slice when domain functionality gives roles semantic meaning.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — New developer can run the whole solution locally on first day (Priority: P1)

A new engineer joins the BusTerminal project. They follow the local development guide and, without any deep Azure familiarity, get the frontend and backend running on their machine, talking to each other, with deterministic results. Their local environment does not depend on a shared developer cloud account being available.

**Why this priority**: This is the smallest delivered increment that gives the project measurable value. Everything else in the foundation derives from "the application runs." Without this, no other slice can be implemented productively.

**Independent Test**: A fresh clone, a documented bootstrap command, and the local startup script produce a frontend served locally and a backend served locally that successfully call one another over a documented health endpoint within the documented time budget. Verifiable by an engineer who has never seen the repo.

**Acceptance Scenarios**:

1. **Given** a fresh clone of the repository on a supported developer machine with the documented prerequisites installed, **When** the developer runs the documented bootstrap script, **Then** all required local dependencies install successfully without manual intervention.
2. **Given** a bootstrapped repository, **When** the developer runs the documented "start local stack" command, **Then** both the frontend and backend are available on documented local URLs within a few minutes and report healthy.
3. **Given** the local stack is running, **When** the developer opens the frontend, **Then** the page successfully exercises the backend through the documented health/diagnostic flow without errors and with telemetry/log output visible in the local terminal.
4. **Given** the local stack is running, **When** the developer makes a change to either frontend or backend code, **Then** the change is reflected without requiring full-stack restarts (hot reload or fast restart, per workload type).

---

### User Story 2 — Frontend and backend deploy automatically to a real Azure environment (Priority: P1)

A maintainer merges to the main branch. A CI/CD pipeline builds container images, publishes them, runs OpenTofu to provision/update Azure infrastructure for the development environment, and deploys the new container images. The deployed frontend serves traffic publicly; the deployed backend is reachable from the frontend; both report healthy.

**Why this priority**: P1 with US1 because together they prove the platform exists in two forms (local and cloud) — neither alone is sufficient. Without an automated cloud deployment, the project cannot ship anything, ever.

**Independent Test**: Trigger the pipeline against a clean Azure subscription (or a clean environment within it). At the end, the deployed frontend's public URL renders, the deployed backend's health endpoint returns success, and no human ran any Azure CLI command by hand.

**Acceptance Scenarios**:

1. **Given** the main branch has a fresh commit, **When** the deployment pipeline runs, **Then** it builds container images, runs security scans, validates infrastructure-as-code, applies infrastructure changes, and rolls out new container revisions without manual steps.
2. **Given** the pipeline has completed successfully, **When** a user navigates to the deployed frontend URL, **Then** the application loads the navigation shell, redirects unauthenticated users through Microsoft Entra ID sign-in, and — once signed in — the platform-status page successfully calls the backend's `whoami` endpoint and renders the user's identity plus the request correlation identifier.
3. **Given** a deployed environment, **When** an operator runs the pipeline a second time without code changes, **Then** infrastructure remains stable (idempotent), no orphaned resources are created, and the deployment completes without errors.
4. **Given** the pipeline authenticates to Azure, **When** the authentication step runs, **Then** it uses workload identity federation with no static cloud credentials stored in the repository or in pipeline secrets.

---

### User Story 3 — Operator can observe a deployed environment (Priority: P2)

An operator opens the centralized telemetry dashboards for the deployed environment and can see: structured logs from both frontend and backend, request traces that span the frontend→backend boundary, health endpoint results, and basic platform metrics. When something fails, they can correlate frontend events to backend events via a shared correlation identifier.

**Why this priority**: P2 because deployment without observability is operationally unsafe but the workloads can still ship without it for a brief window. Required for any production-grade follow-on slice.

**Independent Test**: Generate a request from the deployed frontend. Within the documented telemetry latency window, locate the request in the centralized telemetry store and trace it across frontend and backend using a single correlation identifier.

**Acceptance Scenarios**:

1. **Given** a deployed environment, **When** a user interacts with the frontend, **Then** structured logs from both workloads, with W3C-compliant trace correlation, appear in the central telemetry store.
2. **Given** a request originated in the frontend, **When** an operator searches by correlation identifier, **Then** they can see the full request path from frontend through backend.
3. **Given** the environment is running, **When** an operator views the platform dashboards, **Then** they can see health endpoint status, container revision health, and request volume/latency at a glance.
4. **Given** no user activity is occurring, **When** the operator inspects telemetry, **Then** no personally identifiable information appears — only correlation identifiers and operational metadata.

---

### User Story 4 — Security reviewer can verify the platform is "secure by default" (Priority: P2)

A security reviewer audits the repository and the deployed environment. They confirm: no secrets in source control, no secrets in container images, no static cloud credentials in pipeline configuration, managed identities are used for service-to-service auth, all configured secrets are sourced from a centralized vault, and the Entra ID authentication flow is enforced for human users.

**Why this priority**: P2 because the security guarantees must be in place before any domain functionality is added, but a brief window for hardening between US1/US2 and security review is acceptable for a foundation slice.

**Independent Test**: Run an automated secret-scan over the repository and produce zero findings. Inspect each deployed workload's identity assignment and confirm it uses a managed identity. Inspect the pipeline's Azure authentication and confirm it uses federated workload identity, not stored credentials.

**Acceptance Scenarios**:

1. **Given** the repository, **When** a secret scanner runs, **Then** no secret values are found anywhere — not in source files, not in infrastructure-as-code variable defaults, not in pipeline configuration, not in container build context.
2. **Given** a deployed workload, **When** the workload reads a configuration secret, **Then** it does so via its managed identity and a centralized vault — not via embedded credentials or environment-baked secrets.
3. **Given** the deployment pipeline, **When** it authenticates to Azure, **Then** it uses OIDC workload identity federation and no client secret or service principal password is stored anywhere in the project.
4. **Given** the deployed frontend, **When** an unauthenticated user attempts to reach a protected route, **Then** they are redirected through the Microsoft Entra ID sign-in flow.

---

### User Story 5 — Infrastructure engineer can stand up additional environments reproducibly (Priority: P3)

An infrastructure engineer needs a `test` environment in addition to `dev`. They duplicate the documented environment definition, parameterize it appropriately, run the same pipeline against the new environment, and end up with a functionally identical environment isolated from the others (separate state, separate identities, separate resources).

**Why this priority**: P3 because v1 of the foundation can ship with `dev` only and still deliver the platform value; test/prod can be added incrementally without re-architecting. However, the *pattern* must be in place so the addition is a config change, not a redesign.

**Independent Test**: Following the documented procedure, a second environment can be stood up without modifying any infrastructure module — only environment-specific variables. The two environments operate independently (state isolation, resource isolation, identity isolation).

**Acceptance Scenarios**:

1. **Given** the existing `dev` environment definition, **When** the engineer creates a new environment definition by following documentation, **Then** no shared infrastructure module needs to be modified.
2. **Given** two environments exist, **When** an operation runs against environment A, **Then** it has no effect on environment B's state, resources, or identities.
3. **Given** an existing environment, **When** infrastructure changes are applied, **Then** the operation is idempotent: re-running with no source change produces no changes.

---

### Edge Cases

- **Pipeline failure mid-deploy**: When the infrastructure step succeeds but the container rollout fails, the system MUST leave the environment in a consistent state — the previous container revision MUST continue serving traffic and the pipeline MUST surface the failure clearly.
- **Secret rotation**: When a secret stored in the centralized vault is rotated, workloads MUST pick up the new value without redeployment (subject to the platform's standard refresh window).
- **Cold start under scale-to-zero**: When a workload that has scaled to zero receives a new request, it MUST respond within the documented cold-start budget; observability MUST capture the cold-start event distinctly from warm requests.
- **Local dev with no Azure credentials**: When a developer runs the local stack with no Azure account access, the application MUST start and function for non-cloud-dependent flows; cloud-dependent features MUST degrade gracefully with clear messaging.
- **Telemetry endpoint unreachable**: When the centralized telemetry destination is temporarily unreachable, workloads MUST continue serving requests; telemetry buffering and back-off behavior MUST not destabilize the application.
- **Configuration drift**: When someone modifies an Azure resource outside the infrastructure-as-code pipeline, the next pipeline run MUST detect the drift and either correct it or fail loudly — silent acceptance is not acceptable.
- **Identity provider outage**: When Microsoft Entra ID is unavailable, the frontend MUST present a clear, accessible error state to the user rather than a generic failure.
- **Token expiration during long sessions**: When a user's access token expires while they are using the application, the application MUST silently refresh or prompt re-authentication without data loss.

---

## Requirements *(mandatory)*

> **Note on technology choices**: This is a foundation slice; specific technology selections (e.g., the chosen web framework, runtime, IaC tool, hosting platform) are governed by the BusTerminal Constitution and the Tech Stack reference (`speckit-artifacts/tech-stack.md`). Requirements below describe *capabilities and outcomes*; the planning phase will map them to the approved stack.

### Functional Requirements

#### Repository & Code Structure

- **FR-001**: The repository MUST contain a clear, documented top-level layout that separates frontend application code, backend application code, shared code, infrastructure-as-code definitions, CI/CD workflow definitions, and documentation.
- **FR-002**: The repository MUST contain operational scripts for: environment bootstrap, local startup, test execution, linting, formatting, and infrastructure validation.
- **FR-003**: The repository MUST contain documentation sufficient for a new contributor to onboard and successfully run the solution locally without tribal knowledge.

#### Local Development

- **FR-010**: The solution MUST be runnable end-to-end on a developer's local machine using documented prerequisites.
- **FR-011**: Local execution MUST support both the frontend and backend running natively, and (separately) running each as a container.
- **FR-012**: Local execution MUST NOT require live Azure resources for non-cloud-dependent flows.
- **FR-013**: Local execution MUST surface structured logs and telemetry to the developer's terminal (or local viewer) in real time.

#### Frontend Capabilities

- **FR-020**: The frontend MUST be a server-rendered web application with the architectural defaults required by the constitution (server-by-default rendering, accessible UI primitives, dark mode primary, responsive layouts, prepared for progressive enhancement).
- **FR-021**: The frontend MUST use typed, centralized HTTP clients to call the backend, with token propagation built in.
- **FR-022**: The frontend MUST propagate W3C Trace Context (`traceparent`/`tracestate`) on every outbound HTTP request regardless of which telemetry adapter is active.
- **FR-023**: The frontend MUST integrate with a pluggable observability adapter (default no-op) capable of being switched to the production telemetry sink via environment configuration only — no code change required.
- **FR-024**: The frontend MUST enforce that protected routes redirect unauthenticated users through the configured identity provider sign-in flow.
- **FR-025**: The frontend MUST render a navigation shell — header (product logo, theme toggle, signed-in user menu with sign-out), and a sidebar/nav region that future features will populate — consuming the design tokens and primitives produced by the brand and design foundation slice (`001-brand-system-and-design-foundation`). No new design work is introduced in this slice.
- **FR-026**: The frontend MUST include one authenticated "platform status" page that calls the backend's identity/health endpoint on the user's behalf and surfaces the round-trip's correlation identifier on screen, providing a visible end-to-end proof that sign-in, token exchange, backend call, and trace propagation all work in the deployed environment.

#### Backend Capabilities

- **FR-030**: The backend MUST expose a public, machine-readable API description (OpenAPI specification) generated from the running implementation.
- **FR-030a**: The backend MUST expose an authenticated identity/health endpoint (e.g., `whoami`) that returns the calling principal's display identity and echoes the inbound correlation identifier, enabling the frontend's platform-status page to demonstrate end-to-end sign-in + token validation + trace propagation.
- **FR-031**: The backend MUST organize endpoints by feature area following the vertical-slice architectural style required by the constitution.
- **FR-032**: The backend MUST validate bearer tokens issued by the configured identity provider and reject unauthorized requests with appropriate, non-leaky responses.
- **FR-033**: The backend MUST emit structured logs, distributed traces, and metrics to the centralized telemetry destination, accepting and propagating W3C Trace Context from upstream callers.
- **FR-034**: The backend MUST support resolving configuration from local files, environment variables, and the centralized secret store, with deterministic precedence.
- **FR-035**: The backend MUST authenticate to Azure resources (when reachable) using its managed identity, falling back gracefully for local development.

#### Identity & Access

- **FR-040**: The platform MUST use Microsoft Entra ID as the authentication authority for human users.
- **FR-041**: The platform MUST support interactive sign-in, API bearer token validation, and managed identity authentication for service-to-service flows in this slice.
- **FR-042**: Workloads MUST be assigned managed identities for all Azure resource access (vault, telemetry, future data services). Static credentials MUST NOT be used.
- **FR-043**: The pipeline MUST authenticate to Azure via workload identity federation only. Static client secrets or service principal passwords MUST NOT exist anywhere in the project.

#### Secrets & Configuration

- **FR-050**: Secrets MUST originate from a centralized Azure-native secret store, injected into workloads via the platform's secret-reference or environment-injection facilities.
- **FR-051**: Secrets MUST NOT appear in source control, in container images, in infrastructure-as-code variable defaults, or in pipeline configuration.
- **FR-052**: Automated secret scanning MUST run as part of the pipeline and MUST fail the build on a finding.

#### Hosting Platform

- **FR-060**: Both the frontend and the backend MUST be deployed as containers to the chosen managed container hosting platform.
- **FR-061**: Workloads MUST support configurable minimum/maximum replicas, scale-to-zero where appropriate, and HTTP-based scaling.
- **FR-062**: The frontend MUST expose external ingress. The backend MUST expose external ingress on the public internet, with every request requiring a valid Microsoft Entra ID bearer token; unauthenticated requests MUST be rejected before reaching application logic.
- **FR-063**: Every workload MUST expose distinct liveness, readiness, and startup health endpoints conforming to the hosting platform's probe semantics.

#### Observability

- **FR-070**: All workloads MUST emit structured logs with correlation identifiers attached to every log entry.
- **FR-071**: All workloads MUST emit distributed traces compatible with the centralized telemetry destination.
- **FR-072**: All Azure resources provisioned by the platform MUST route their diagnostic logs to the shared Log Analytics Workspace.
- **FR-073**: Personally identifiable information MUST NOT appear in default telemetry. Only correlation identifiers and operational metadata propagate unless explicitly opted in by a future spec.
- **FR-074**: Health endpoint results MUST be observable in the centralized telemetry destination.

#### Infrastructure as Code

- **FR-080**: All Azure infrastructure for the platform MUST be defined as infrastructure-as-code using OpenTofu, with no alternative IaC technology introduced.
- **FR-081**: Infrastructure modules MUST be composable across environments and parameterizable for environment-specific values.
- **FR-082**: For all shared/CI usage (every deployed environment), OpenTofu state MUST be stored remotely with state locking enabled; environments MUST have isolated remote state. Local developer workflows MAY use local state and MUST NOT require remote-state access from a developer's machine.
- **FR-082a**: A dedicated one-time `platform-bootstrap` OpenTofu module MUST exist that provisions: the remote-state storage account, the state container, state-locking configuration, and the GitHub Actions federated identity used by the pipeline. This module MUST be idempotent and re-runnable.
- **FR-082b**: The repository MUST document the bootstrap setup TWO ways: (a) running the one-time `platform-bootstrap` OpenTofu module, and (b) a step-by-step manual procedure (e.g., `az` CLI commands or portal walkthrough) that produces a functionally equivalent backend. Both paths MUST end in a working remote-state configuration usable by the pipeline.
- **FR-083**: Environment-specific values MUST be parameterized — no hardcoded environment values in module source.
- **FR-084**: Infrastructure MUST be idempotent: re-applying with no source change produces no changes.

#### CI/CD

- **FR-090**: A continuous-integration pipeline MUST run on every change, performing at minimum: build, unit test, linting, container image build, container/dependency security scanning, and IaC validation.
- **FR-091**: A continuous-deployment pipeline MUST be capable of deploying to the development environment automatically on merge to the main branch.
- **FR-092**: The pipelines MUST support an environment-promotion model so test and production environments can be added later by configuration, not by re-implementation.
- **FR-093**: Deployment MUST support progressive rollout patterns offered by the hosting platform and MUST be capable of rollback to a previous revision.

#### Operational Readiness

- **FR-100**: Every environment provisioned by this foundation MUST have its own isolated identities, isolated state, isolated resources, and isolated secrets.
- **FR-101**: All cross-boundary HTTP calls (frontend→backend, pipeline→Azure, workload→Azure) MUST be observable in the centralized telemetry destination with correlation identifiers preserved end-to-end.

### Key Entities

> This slice does not introduce domain entities. The "entities" here are the durable platform constructs that future slices depend on.

- **Environment**: A named, isolated deployment target (initially `dev`; extensible to `test`, `prod`). Owns its own infrastructure state, resource group(s), managed identities, secret store, and telemetry namespace.
- **Workload**: A deployable container (frontend, backend) running on the managed container platform. Owns a managed identity, a configured ingress posture, defined scaling bounds, and the standard health probe set.
- **Identity Configuration**: The Entra ID application registration(s) and managed identity assignments enabling interactive user authentication, API token validation, and service-to-service authentication.
- **Pipeline Run**: A single execution of a CI or CD workflow tied to a commit, producing build artifacts, deployable images, and (for CD) deployed revisions. Owns a federated identity for cloud access.
- **Telemetry Stream**: The aggregated logs, traces, metrics, and diagnostics flowing into the centralized destination for a given environment, correlated across workloads via shared trace context.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new developer following the documented onboarding can have the solution running locally end-to-end in under 30 minutes from a fresh clone on a supported machine.
- **SC-002**: A merge to the main branch results in a deployed, healthy environment without manual intervention, completing in under 20 minutes for a no-infrastructure-change deployment.
- **SC-003**: 100% of requests originated in the frontend can be correlated to their corresponding backend activity in the centralized telemetry destination within the telemetry latency window (typically under 2 minutes).
- **SC-004**: Zero secret findings in automated repository scanning over the lifetime of this foundation slice.
- **SC-005**: 100% of workload-to-Azure authentication uses managed identity; 100% of pipeline-to-Azure authentication uses workload identity federation. No static cloud credentials anywhere in the project.
- **SC-006**: A second environment (e.g. `test`) can be added by an engineer who has not worked on the foundation in under 1 working day, using only documented procedures and environment-level configuration changes.
- **SC-007**: Liveness, readiness, and startup endpoints respond within their documented timeouts on every deployed workload, and their results are visible in the centralized telemetry destination.
- **SC-008**: Re-running the deployment pipeline with no source change produces no infrastructure changes (idempotency confirmed) on every executed run.
- **SC-009**: All Azure resources provisioned by this slice route diagnostic logs to the shared Log Analytics Workspace — verified by inventory check at the close of this slice.
- **SC-010**: Authenticated end-to-end user flow (sign-in → frontend → backend) succeeds in the deployed development environment on the first attempt after a clean deployment.

---

## Assumptions

- **Sequencing**: The brand system and design foundation slice (`001-brand-system-and-design-foundation`) is the design substrate; this slice consumes those tokens and primitives rather than redefining them. Where the two slices overlap, the design foundation wins for visual decisions and this slice wins for runtime/operational decisions.
- **Environment scope for this slice**: The `dev` environment is provisioned end-to-end. The `test` and `prod` environment definitions are scaffolded (folder structure, parameter file templates) and the pattern is proven, but their resources are NOT provisioned in this slice. Adding them later MUST be a configuration change only — no re-architecting, no module changes. (See Clarifications, 2026-05-16.)
- **Identity scope for this slice**: Interactive sign-in works end-to-end against a real Microsoft Entra ID tenant. Authorization is binary (authenticated vs. unauthenticated) — any signed-in user can access any protected endpoint. Application roles, group claims, group-based RBAC enforcement, and fine-grained authorization rules are explicitly out of scope and covered by a later slice. (See Clarifications, 2026-05-16.)
- **Hosting choice**: The hosting platform is Azure Container Apps, fixed by the constitution; the spec does not re-derive it.
- **IaC choice**: OpenTofu is the only IaC technology, fixed by the constitution; alternatives are prohibited without an ADR.
- **Custom domains / TLS**: The development environment uses the hosting platform's default domain. Custom domain + certificate management is out of scope for this slice but the design MUST NOT preclude adding them later.
- **CI/CD provider**: GitHub Actions, per the constitution.
- **Browser support**: Last two major versions of Chrome/Edge/Firefox/Safari (desktop) plus iPadOS Safari and Android Chrome, per the constitution's browser policy.
- **Telemetry adapter default**: The frontend ships with the no-op observability adapter active by default in local development. The Azure-native adapter is activated via environment configuration in the deployed environment.
- **Frontend → backend calling pattern**: Because the backend has public, token-protected ingress, the frontend may call it from either server components (using a delegated/on-behalf-of token) or — when justified — from client-side code with the user's access token attached. The planning phase chooses the default pattern; both must be supportable.
- **OpenTofu state strategy**: Shared/CI work uses remote state (bootstrapped by the `platform-bootstrap` module); local developer workflows use local state for any local `tofu` runs and MUST NOT require access to the remote backend. Devs are not expected to apply OpenTofu against shared environments from their machines.
- **No domain functionality**: No Service Bus registry features, no ingestion, no AI capabilities, no advanced RBAC, no multi-tenant isolation, no message brokering — these are explicit non-goals of the source artifact and remain non-goals here.
- **Dev shell**: Local dev scripts are provided for PowerShell (primary) and bash (secondary) per the project's platform conventions.

---

## Dependencies

- **Microsoft Entra ID tenant** must be available with sufficient permission to register applications and configure federated credentials.
- **Azure subscription** must be available with sufficient permission to create resource groups, managed identities, Container Apps environments, Key Vault, Log Analytics, Application Insights, and Container Registry resources.
- **GitHub repository** must be configured with the permissions necessary to use OIDC workload identity federation to Azure.
- **Constitution and tech-stack reference** (`.specify/memory/constitution.md`, `speckit-artifacts/tech-stack.md`) are the source of truth for technology choices; this slice does not introduce new technologies beyond what those documents already approve.

---

## Out of Scope

The following are explicit non-goals of this slice and MUST NOT be implemented here:

- Service Bus registry, discovery, governance, or observability features
- Ingestion of Azure Service Bus topology data
- AI capabilities of any kind
- Role-based access control of any kind — app roles, group claims, role-to-permission mapping, role-gated endpoints, or any authorization beyond the binary authenticated/unauthenticated check
- Production-scale tuning, autoscaling policy refinement, capacity planning
- Multi-region failover or geo-redundancy
- Tenant isolation / multi-tenant architecture
- Complex workflow orchestration
- Service Bus message brokering, transport replacement, or runtime interception
- Custom domains and TLS certificate management (deferred)
- Blue/green or canary deployment automation (the pattern is supportable but not implemented)
- Production environment provisioning (the pattern is supportable but the resources are not created in this slice)
