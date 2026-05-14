# BusTerminal Constitution

Version: 1.0  
Status: Draft Foundation Constitution  
Project: BusTerminal  

---

# 1. Purpose

BusTerminal is an open-source Azure-native service bus registry and governance platform designed to provide discovery, documentation, visibility, governance, operational intelligence, and developer experience improvements for Azure messaging ecosystems.

The platform exists to:

- Centralize Azure Service Bus topology metadata
- Improve discoverability of messaging assets
- Enable governance and operational standards
- Provide searchable documentation and relationships
- Reduce organizational messaging sprawl
- Accelerate onboarding and integration efforts
- Support future extensibility into broader messaging ecosystems

BusTerminal is intentionally designed as:

- Single-tenant by default
- Cloud-aware but Azure-first
- Containerized and portable
- API-first
- Automation-friendly
- Open-source oriented
- Enterprise-operable

---

# 2. Foundational Engineering Principles

## 2.1 Azure-First Architecture

BusTerminal is optimized first for Microsoft Azure.

Architectural decisions should:

- Favor Azure PaaS services where operationally beneficial
- Integrate deeply with Azure identity and management APIs
- Align with Azure Well-Architected Framework principles
- Prioritize secure-by-default Azure networking patterns
- Support enterprise-scale Azure deployments

The platform may evolve toward broader cloud compatibility over time, but Azure remains the primary target platform.

---

## 2.2 API-First Design

All core platform functionality must be accessible through documented APIs.

Requirements:

- APIs are treated as first-class products
- Public contracts must be versioned
- Internal UI functionality should consume the same APIs when practical
- OpenAPI specifications must be generated and maintained
- APIs should be automation-friendly and CI/CD compatible

---

## 2.3 Strong Domain Modeling

Messaging entities are treated as authoritative domain concepts.

The platform should model:

- Namespaces
- Queues
- Topics
- Subscriptions
- Rules
- Connections
- Ownership metadata
- Operational metadata
- Contracts and schemas
- Environment relationships
- Dependency graphs

Domain terminology must remain consistent across:

- APIs
- UI
- Documentation
- Database models
- Search indexes
- Telemetry

---

## 2.4 Security by Default

Security is a foundational requirement.

Requirements:

- Private networking preferred wherever feasible
- Least-privilege RBAC
- Managed identity preferred over secrets
- Entra ID authentication required for platform access
- Secure defaults must be enforced
- Secrets must never be committed to source control
- Supply-chain security and dependency hygiene are mandatory

---

## 2.5 Operational Excellence

Operational visibility is a core product feature.

All services must provide:

- Structured logging
- Distributed tracing
- Metrics
- Health endpoints
- Diagnostic correlation
- Operational dashboards
- Failure visibility

The platform must be operable by small engineering teams.

---

## 2.6 Incremental Extensibility

BusTerminal must support future extensibility without requiring architectural rewrites.

Examples include:

- Additional messaging systems
- SaaS evolution
- Plugin integrations
- AI-assisted discovery
- Contract validation
- Governance workflows
- Event lineage
- CLI tooling

Architectural decisions should preserve future optionality where reasonable.

---

# 3. Technology Standards

## 3.1 Backend Standards

Primary backend stack:

- .NET 10
- C#
- ASP.NET Core
- Minimal APIs where appropriate
- Vertical Slice Architecture preferred
- Dependency Injection via built-in container unless requirements justify alternatives

### Backend Language Standards

Modern C# features should be preferred where they improve:

- Readability
- Maintainability
- Performance
- Developer productivity

Examples include:

- Primary constructors
- Collection expressions
- Required members
- Pattern matching
- Record types
- File-scoped namespaces
- Typed results
- Async streams
- Nullable reference types

Avoid unnecessary abstraction layers and framework complexity.

### Backend Design Principles

Backend services should:

- Be stateless where practical
- Favor composition over inheritance
- Minimize hidden magic
- Prefer explicitness over convention-heavy frameworks
- Separate domain logic from infrastructure concerns
- Emphasize testability

---

## 3.2 Frontend Standards

Primary frontend stack:

- Next.js 16.x
- React
- TypeScript
- shadcn/ui
- Tailwind CSS

### Frontend UX Principles

The BusTerminal UI is a product feature, not merely an administration shell.

The frontend should prioritize:

- Excellent information density
- Fast navigation
- Responsive interactions
- Accessibility
- Enterprise-grade usability
- High-quality visual polish
- Dark mode support
- Keyboard-friendly workflows
- Consistent design systems

### Frontend Engineering Principles

Frontend code should:

- Favor server components where appropriate
- Minimize unnecessary client-side complexity
- Strongly type API interactions
- Use reusable UI primitives
- Avoid monolithic component patterns
- Maintain strict design consistency
- Support future extensibility

---

## 3.3 Data Platform Standards

Primary persistence stack:

- Azure Cosmos DB for metadata storage
- Azure AI Search for indexing and discovery

Data architecture principles:

- Denormalization is acceptable where beneficial
- Searchability is a first-class concern
- Schemas must evolve safely
- Partitioning strategies should preserve future scalability
- Metadata lineage should remain traceable

---

## 3.4 Hosting and Infrastructure Standards

Primary hosting stack:

- Azure Container Apps
- Azure Container Registry

Primary Infrastructure as Code tooling:

- OpenTofu

Infrastructure principles:

- OpenTofu is the required Infrastructure as Code tool for BusTerminal
- Bicep must not be used for BusTerminal infrastructure unless explicitly approved by architectural exception
- Reproducible deployments are mandatory
- Environment parity is strongly preferred
- Secure networking defaults are required
- Observability must be integrated from the beginning
- Infrastructure modules must be versioned and reviewed like application code

### Azure Verified Modules

Azure Verified Modules should be preferred for Azure resource deployment when suitable modules are available for the target service and deployment scenario.

AVM usage requirements:

- Prefer AVM modules over hand-authored raw resource definitions where the module supports the required configuration
- Validate that the selected AVM module supports OpenTofu execution in the BusTerminal toolchain before adoption
- Pin module versions explicitly
- Document any deviation from AVM usage in the relevant spec or architecture decision record
- Exceptions are allowed when AVM is not available, does not support the required deployment configuration, lacks required service coverage, blocks secure networking requirements, or introduces unreasonable complexity

When an AVM exception is required, OpenTofu-native resource definitions or carefully scoped custom modules may be used.

---

## 3.5 Identity and Authentication

Primary identity platform:

- Microsoft Entra ID

Requirements:

- Managed identity preferred for service-to-service communication
- Role-based access control enforced
- No embedded credentials
- Local development flows must remain developer-friendly

---

# 4. Architecture Principles

## 4.1 Modular Monolith First

BusTerminal should initially favor a modular monolith architecture.

Microservices should only be introduced when justified by:

- Scaling boundaries
- Team ownership boundaries
- Deployment independence requirements
- Operational isolation needs

Premature service decomposition is prohibited.

---

## 4.2 Container-Native Architecture

All services must:

- Run in containers
- Support local containerized development
- Support reproducible builds
- Be CI/CD friendly

---

## 4.3 Async-First Thinking

The platform itself should embrace asynchronous patterns where beneficial.

Examples:

- Background discovery
- Indexing
- Topology scanning
- Telemetry ingestion
- AI enrichment

---

## 4.4 AI-Assisted Capability Enablement

BusTerminal is expected to evolve into an AI-enhanced platform.

Architecture should support:

- Semantic search
- AI-assisted discovery
- Topology summarization
- Documentation generation
- Operational insights
- Governance recommendations

AI features must remain explainable and operationally observable.

---

# 5. Engineering Workflow Standards

## 5.1 Spec-Driven Development

All significant functionality must begin with specifications.

Specifications should:

- Define business intent
- Define architectural constraints
- Define acceptance criteria
- Define operational requirements
- Define security requirements
- Define UX expectations

Specs should be independently implementable where possible.

---

## 5.2 CI/CD Requirements

All pull requests should validate:

- Build success
- Unit tests
- Linting
- Formatting
- Security scanning
- Dependency vulnerability scanning

Main branch deployments should remain continuously deployable.

---

## 5.3 Testing Standards

Testing strategy should include:

- Unit tests
- Integration tests
- Contract tests
- UI component testing
- End-to-end smoke validation

Overly brittle tests should be avoided.

---

# 6. Frontend AI Tooling Standards

## 6.1 MCP Usage for Development Workflows

Development agents and AI-assisted coding workflows should use the following MCP servers where applicable:

- Next.js MCP server
- shadcn/ui MCP server
- Microsoft Learn MCP server
- Context7 MCP server

Purpose of usage:

- Current framework compatibility
- Accurate API and component usage
- Access to authoritative Microsoft platform documentation
- Improved architecture guidance
- Alignment with framework best practices
- Reduction of hallucinated APIs, SDKs, and implementation patterns
- Improved consistency across contributors and coding agents

MCP integrations are considered engineering workflow standards rather than runtime architecture requirements.


---

# 7. Non-Goals

The following are explicitly out of scope for the foundational platform phase:

- Full enterprise ESB functionality
- Message brokering
- Message transport replacement
- Runtime traffic interception
- Full multi-cloud abstraction layers
- Multi-tenant SaaS architecture
- Complex workflow orchestration
- Legacy on-premise broker management

These may evolve later but are not foundational requirements.

---

# 8. Decision Framework

When making architectural decisions, prioritize:

1. Operational simplicity
2. Developer productivity
3. Maintainability
4. Security
5. Observability
6. Extensibility
7. Performance
8. Cost efficiency

Avoid premature optimization.

Favor pragmatic engineering over theoretical purity.

---

# 9. Open Source Principles

BusTerminal is intended to support open-source community participation.

Requirements:

- Clear documentation
- Reproducible local development
- Contributor-friendly onboarding
- Transparent architectural decisions
- Consistent coding standards
- Public issue tracking
- Automated validation where practical

---

# 10. Constitutional Authority

This constitution defines the foundational engineering and architectural standards for BusTerminal.

All specifications, implementation plans, and technical decisions should align with these principles unless explicitly superseded through formal architectural review.

