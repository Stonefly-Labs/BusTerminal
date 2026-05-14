<!--
SYNC IMPACT REPORT
==================
Version change: 0.0.0 (template placeholder) → 1.0.0 (initial ratification)
Bump rationale: First substantive ratification of the BusTerminal Constitution,
replacing the unfilled Spec Kit template. Treated as a MAJOR release because
this is the inaugural authoritative governance document — there is no prior
populated version to maintain compatibility with.

Source: speckit-artifacts/busterminal-constitution.md (Draft Foundation v1.0)

Modified principles (all newly populated):
  - I. Azure-First Architecture
  - II. API-First Design
  - III. Strong Domain Modeling
  - IV. Security by Default
  - V. Operational Excellence
  - VI. Incremental Extensibility

Added sections:
  - Technology Standards (backend, frontend, data, hosting/IaC, identity)
  - Architecture Standards (modular monolith first, container-native,
    async-first, AI-assisted capability enablement)
  - Engineering Workflow & Quality Standards (spec-driven development, CI/CD,
    testing, AI tooling/MCP usage)
  - Scope & Decision Framework (non-goals, decision priorities, open-source
    commitments)
  - Governance (authority, amendment procedure, versioning policy,
    compliance review)

Removed sections: none (template placeholders replaced wholesale).

Templates requiring updates:
  - .specify/templates/plan-template.md ✅ aligned (Constitution Check section
    is intentionally generic — `/speckit-plan` derives gates from this file at
    plan time; no edits required).
  - .specify/templates/spec-template.md ✅ aligned (no constitution-specific
    scope changes required).
  - .specify/templates/tasks-template.md ✅ aligned (existing task categories
    for setup, foundational, story, and polish accommodate the
    observability, security, and testing principles defined here).
  - .specify/templates/checklist-template.md ✅ aligned (generic checklist
    scaffold; no edits required).
  - .specify/extensions.yml ✅ aligned (git auto-commit hooks support the
    spec-driven workflow).
  - README.md ⚠ pending — no project README exists yet; create one referencing
    this constitution when the foundational repo scaffolding lands.

Deferred items / follow-up TODOs:
  - TODO(README): Author a top-level README.md that links to this constitution
    and summarizes the technology stack.
  - TODO(ADR): Establish an `docs/adr/` location for Architectural Decision
    Records once the first deviation (e.g., an AVM exception) is recorded.
-->

# BusTerminal Constitution

## Core Principles

### I. Azure-First Architecture

BusTerminal MUST be optimized first for Microsoft Azure. Architectural
decisions MUST favor Azure PaaS services where operationally beneficial,
integrate deeply with Azure identity and management APIs, align with the
Azure Well-Architected Framework, prioritize secure-by-default Azure
networking patterns, and support enterprise-scale Azure deployments.

The platform MAY evolve toward broader cloud compatibility over time, but
Azure remains the primary target platform. Multi-cloud abstraction layers
are explicitly out of scope (see "Scope & Decision Framework").

**Rationale**: A focused primary platform avoids the lowest-common-denominator
compromises of premature cloud abstraction and lets the platform deliver
deep operational value to Azure customers — its primary audience.

### II. API-First Design

All core platform functionality MUST be accessible through documented APIs.

- APIs MUST be treated as first-class products with versioned public
  contracts.
- OpenAPI specifications MUST be generated and maintained for every public
  API surface.
- Internal UI functionality SHOULD consume the same APIs whenever
  practical; UI-only backdoors that bypass the public contract are
  prohibited without a documented exception.
- Public APIs MUST be automation-friendly and CI/CD compatible.

**Rationale**: Treating the API as the product (rather than the UI) preserves
optionality for CLI tooling, integrations, and automation workflows, and
forces clean separation between presentation and platform logic.

### III. Strong Domain Modeling

Messaging entities MUST be modeled as authoritative domain concepts.
Required entities include: Namespaces, Queues, Topics, Subscriptions, Rules,
Connections, Ownership metadata, Operational metadata, Contracts/Schemas,
Environment relationships, and Dependency graphs.

Domain terminology MUST remain consistent across APIs, UI, documentation,
database models, search indexes, and telemetry. Synonym drift between
layers is treated as a defect.

**Rationale**: BusTerminal's product value comes from being the authoritative
registry for messaging topology. Inconsistent vocabulary across layers
erodes that authority and frustrates discovery.

### IV. Security by Default

Security is a foundational, non-negotiable requirement:

- Private networking MUST be preferred wherever feasible.
- Access controls MUST follow least-privilege RBAC.
- Managed identity MUST be preferred over secrets for service-to-service
  authentication.
- Microsoft Entra ID authentication MUST be required for platform access.
- Secure defaults MUST be enforced; opt-in insecurity is prohibited.
- Secrets MUST NEVER be committed to source control.
- Supply-chain security and dependency hygiene (SCA scanning, pinned
  versions, signed builds where applicable) are mandatory.

**Rationale**: BusTerminal stores topology and ownership metadata for
enterprise messaging. A breach exposes the connection blueprint of the
organization, so security cannot be retrofitted.

### V. Operational Excellence

Operational visibility is a core product feature, not an afterthought. All
services MUST provide:

- Structured logging
- Distributed tracing
- Metrics
- Health endpoints
- Diagnostic correlation (request/correlation IDs propagated end-to-end)
- Operational dashboards
- Explicit failure visibility (no silent retries that mask systemic issues)

The platform MUST remain operable by small engineering teams.

**Rationale**: A registry that itself becomes opaque defeats its own purpose.
Operability is part of the product surface.

### VI. Incremental Extensibility

BusTerminal MUST support future extensibility without requiring
architectural rewrites. Future-facing capabilities — additional messaging
systems, SaaS evolution, plugin integrations, AI-assisted discovery,
contract validation, governance workflows, event lineage, and CLI tooling —
MUST be enabled by preserving optionality in current designs.

Decisions that close off these directions for short-term convenience
require explicit justification in an Architectural Decision Record (ADR).

**Rationale**: The platform is intentionally seeded as Azure Service Bus
first but is expected to grow. Premature lock-in to a single messaging
provider's idioms would prevent that growth.

## Technology Standards

These standards are binding for new work. Deviations require an ADR.

### Backend

- .NET 10, C#, ASP.NET Core. Minimal APIs SHOULD be preferred where
  practical.  Controllers should not be used unless Minimal API isn't feasible. Vertical Slice Architecture is preferred over horizontal
  layering. Dependency Injection uses the built-in container unless
  requirements justify an alternative.
- Modern C# features (primary constructors, collection expressions,
  required members, pattern matching, records, file-scoped namespaces,
  typed results, async streams, nullable reference types) SHOULD be used
  where they improve readability, maintainability, performance, or
  developer productivity.
- Backend services MUST be stateless where practical, favor composition
  over inheritance, minimize hidden magic, prefer explicitness over
  convention-heavy frameworks, separate domain logic from infrastructure
  concerns, and emphasize testability.
- Unnecessary abstraction layers and framework complexity MUST be avoided.

### Frontend

- Next.js 16.x, React, TypeScript, shadcn/ui, Tailwind CSS.
- The UI is a product feature. It MUST prioritize information density,
  fast navigation, responsive interactions, accessibility, enterprise-grade
  usability, visual polish, dark-mode support, keyboard-friendly workflows,
  and consistent design systems.
- Frontend code SHOULD favor server components where appropriate, minimize
  unnecessary client-side complexity, strongly type API interactions, use
  reusable UI primitives, avoid monolithic component patterns, and
  maintain strict design consistency.

### Data Platform

- Azure Cosmos DB for metadata storage; Azure AI Search for indexing and
  discovery.
- Denormalization is acceptable where beneficial. Searchability is a
  first-class concern. Schemas MUST evolve safely (backward-compatible
  migrations preferred; breaking schema changes require an ADR).
  Partitioning strategies MUST preserve future scalability. Metadata
  lineage MUST remain traceable.

### Hosting and Infrastructure

- Azure Container Apps and Azure Container Registry.
- Azure Key Vault will be used to store any secrets required by the platform.
- Obserability will be handled by Azure Monitor and Application Insights, using OpenTelemetry for Azure Monitor.
  - All Azure services must route all diagnostic logs to the Log Analytics Workspace that will be deployed with this solution.
- Any background processing jobs will run as Azure Container Apps Jobs
- Event driven processing will be implemented as containerized Azure Functions that are deployed on the Container Apps Environment.
  - Ensure that the newest, native Azure Functions for Container Apps hosting is used.
- **OpenTofu is the required Infrastructure as Code tool.** Bicep MUST NOT
  be used for BusTerminal infrastructure unless explicitly approved by an
  ADR-recorded architectural exception.
- Reproducible deployments are mandatory. Environment parity is strongly
  preferred. Secure networking defaults are required. Observability MUST
  be integrated from the beginning. Infrastructure modules MUST be
  versioned and reviewed like application code.
- **Azure Verified Modules (AVM)** SHOULD be preferred over hand-authored
  resource definitions when a suitable module supports the required
  configuration and supports OpenTofu execution. Module versions MUST be
  pinned explicitly. Deviations from AVM usage MUST be documented in the
  relevant spec or ADR. Exceptions are permitted when AVM is unavailable,
  lacks required coverage, blocks secure networking requirements, or
  introduces unreasonable complexity.

### Identity and Authentication

- Microsoft Entra ID is the primary identity platform.
- Managed identity MUST be preferred for service-to-service communication.
- Role-based access control MUST be enforced. Embedded credentials are
  prohibited. Local development flows MUST remain developer-friendly
  (e.g., Azure CLI credential or developer Entra ID sign-in).

## Architecture Standards

### Modular Monolith First

BusTerminal MUST initially favor a modular monolith. Microservices MAY be
introduced only when justified by scaling boundaries, team ownership
boundaries, deployment independence requirements, or operational isolation
needs. Premature service decomposition is prohibited.

### Container-Native

All services MUST run in containers, support local containerized
development, support reproducible builds, and be CI/CD friendly.

### Async-First Thinking

The platform MUST embrace asynchronous patterns where beneficial —
background discovery, indexing, topology scanning, telemetry ingestion,
and AI enrichment in particular.

### AI-Assisted Capability Enablement

The architecture MUST support semantic search, AI-assisted discovery,
topology summarization, documentation generation, operational insights,
and governance recommendations. AI features MUST remain explainable and
operationally observable; opaque "black box" AI behaviors are prohibited
in user-facing capabilities.

## Engineering Workflow & Quality Standards

### Spec-Driven Development

All significant functionality MUST begin with a specification. Specs MUST
define business intent, architectural constraints, acceptance criteria,
operational requirements, security requirements, and UX expectations.
Specs SHOULD be independently implementable where possible.

### CI/CD Requirements

All pull requests MUST validate: build success, unit tests, linting,
formatting, security scanning, and dependency vulnerability scanning.
The main branch MUST remain continuously deployable.

### Testing Standards

The testing strategy MUST include unit tests, integration tests, contract
tests, UI component testing, and end-to-end smoke validation. Overly
brittle tests MUST be avoided — tests SHOULD assert observable behavior,
not implementation detail.

### AI Tooling / MCP Usage

Development agents and AI-assisted coding workflows SHOULD use the
following MCP servers where applicable: Next.js MCP server, shadcn/ui MCP
server, Microsoft Learn MCP server, Context7 MCP server. Purpose: current
framework compatibility, accurate API and component usage, authoritative
Microsoft platform documentation, reduction of hallucinated APIs, and
contributor/agent consistency.

MCP integrations are engineering workflow standards, not runtime
architecture requirements; runtime systems MUST NOT depend on MCP server
availability.

## Scope & Decision Framework

### Non-Goals (Foundational Phase)

The following are explicitly out of scope and MUST NOT drive foundational
design decisions:

- Full enterprise ESB functionality
- Message brokering
- Message transport replacement
- Runtime traffic interception
- Full multi-cloud abstraction layers
- Multi-tenant SaaS architecture
- Complex workflow orchestration
- Legacy on-premise broker management

These MAY be revisited in future phases but are not foundational
requirements.

### Decision Priorities

When making architectural decisions, prioritize in this order:

1. Operational simplicity
2. Developer productivity
3. Maintainability
4. Security
5. Observability
6. Extensibility
7. Performance
8. Cost efficiency

Premature optimization MUST be avoided. Pragmatic engineering MUST be
favored over theoretical purity.

### Open-Source Commitments

BusTerminal is intended to support open-source community participation
and MUST maintain: clear documentation, reproducible local development,
contributor-friendly onboarding, transparent architectural decisions
(via ADRs), consistent coding standards, public issue tracking, and
automated validation where practical.

## Governance

### Authority

This constitution defines the foundational engineering and architectural
standards for BusTerminal. All specifications, implementation plans, and
technical decisions MUST align with these principles unless explicitly
superseded through the amendment procedure below.

### Amendment Procedure

1. Amendments MUST be proposed via a pull request that modifies this
   document.
2. The PR description MUST identify which principle(s) or section(s) are
   affected and MUST classify the change as MAJOR, MINOR, or PATCH.
3. The PR MUST update the Sync Impact Report at the top of this file and
   propagate any required changes to dependent templates under
   `.specify/templates/` and to runtime guidance docs.
4. Amendments MUST be reviewed and approved by a project maintainer
   before merge. Architectural deviations from this constitution that are
   merged without an amendment are treated as defects.

### Versioning Policy

This constitution follows semantic versioning:

- **MAJOR**: Backward-incompatible governance changes or principle
  removals/redefinitions.
- **MINOR**: A new principle or section is added, or existing guidance is
  materially expanded.
- **PATCH**: Clarifications, wording fixes, typo corrections, and other
  non-semantic refinements.

### Compliance Review

- All PRs and code reviews MUST verify constitution compliance.
- The `/speckit-plan` workflow MUST derive its Constitution Check gates
  from this file.
- Complexity that violates a principle MUST be justified in the
  Complexity Tracking section of the relevant plan.
- Exceptions to Technology Standards (e.g., an AVM bypass, a Bicep
  deviation) MUST be recorded as ADRs under `docs/adr/` (see deferred
  TODO in the Sync Impact Report).

### Runtime Guidance

Day-to-day development guidance — directory layout conventions, agent
instructions, and onboarding notes — is maintained alongside the code
(e.g., `CLAUDE.md`, `README.md`, feature-level `quickstart.md` files).
Those documents MUST defer to this constitution on points of conflict.

**Version**: 1.0.0 | **Ratified**: 2026-05-14 | **Last Amended**: 2026-05-14
