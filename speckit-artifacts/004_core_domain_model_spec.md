# 004-core-domain-model

## Overview

Define the canonical BusTerminal domain model including namespaces, entities, resource relationships, versioning semantics, ownership boundaries, lifecycle states, validation rules, and extensibility conventions. This specification establishes the durable metadata contract that all API, UI, indexing, synchronization, governance, and automation capabilities rely upon.

This slice forms the foundational semantic model of BusTerminal and must be treated as a long-lived compatibility boundary.

---

# Goals

## Primary Goals

- Establish the canonical BusTerminal metadata model
- Define all first-class registry resource types
- Create normalized ownership and namespace structures
- Define lifecycle and governance semantics
- Standardize versioning and compatibility behavior
- Support extensibility without schema instability
- Enable future SaaS/multi-tenant evolution without immediate tenancy coupling
- Ensure compatibility with Azure Service Bus concepts
- Create deterministic identifiers and relationship structures
- Support search indexing and graph-style navigation

## Secondary Goals

- Enable future event-driven synchronization
- Support import/export portability
- Enable AI-assisted discovery and enrichment
- Support future federation scenarios
- Provide schema stability guarantees for external integrations

---

# Non-Goals

The following are explicitly out of scope for this specification:

- UI implementation details
- Authentication and authorization enforcement
- Search indexing implementation
- Cosmos DB physical partition strategies
- API route definitions
- Message runtime processing
- Broker provisioning automation
- SaaS tenancy implementation
- Infrastructure deployment
- AI enrichment implementations

---

# Architectural Principles

## Domain-First Design

The domain model is authoritative.

Storage models, API contracts, indexing projections, and UI state representations must derive from the canonical domain model rather than independently evolve.

## Stable Identifiers

All first-class entities must have immutable identifiers.

Names may evolve.
Identifiers may not.

## Human-Centric Metadata

The system must optimize for operational discoverability and maintainability.

Descriptions, ownership, tags, compatibility notes, and governance metadata are first-class concepts.

## Explicit Relationships

Relationships between resources must be modeled explicitly rather than inferred from naming conventions.

## Extensibility Without Forking

Organizations must be able to extend metadata safely without modifying the canonical schema.

## Forward-Compatible Evolution

The domain model must support additive evolution while minimizing breaking changes.

---

# Functional Requirements

## FR-001 Canonical Resource Model

The platform shall define a canonical resource abstraction shared across all first-class resource types.

All resources shall include:

- Immutable identifier
- Resource type
- Name
- Display name
- Description
- Creation metadata
- Last modified metadata
- Tags
- Ownership metadata
- Lifecycle status
- Classification metadata
- Version metadata
- Source metadata
- Validation state
- Extensibility metadata

---

## FR-002 Namespace Model

The platform shall support hierarchical namespaces.

Namespaces shall:

- Support logical organization
- Support nested hierarchy
- Support ownership delegation
- Support metadata inheritance where appropriate
- Support governance boundaries
- Support search scoping

Namespaces shall not:

- Encode infrastructure topology directly
- Depend on Azure resource hierarchy

### Example

```text
enterprise/payments/order-processing
enterprise/logistics/shipping
shared/platform/events
```

---

## FR-003 Resource Types

The following first-class resource types shall exist:

| Resource Type | Description |
|---|---|
| Namespace | Organizational grouping boundary |
| Broker | Logical messaging broker definition |
| Queue | Queue metadata definition |
| Topic | Topic metadata definition |
| Subscription | Topic subscription definition |
| Message Contract | Logical schema or payload definition |
| Consumer Application | Consuming system definition |
| Producer Application | Producing system definition |
| Team | Ownership and governance grouping |
| Environment | Environment classification |
| Tag | Taxonomy metadata |
| Policy | Governance policy metadata |
| Integration Flow | Logical producer/consumer flow definition |
| Documentation Asset | Linked documentation reference |

The system must support future resource type expansion.

---

## FR-004 Queue Model

Queue resources shall support:

- Namespace association
- Environment association
- Queue type classification
- Duplicate detection metadata
- Session requirements metadata
- Ordering metadata
- Partitioning metadata
- Dead-letter behavior metadata
- TTL metadata
- Message size metadata
- Ownership metadata
- Consumer associations
- Producer associations
- Contract associations
- Operational metadata
- Deprecation metadata

---

## FR-005 Topic Model

Topic resources shall support:

- Namespace association
- Subscription relationships
- Contract relationships
- Producer relationships
- Classification metadata
- Ordering metadata
- Partitioning metadata
- Ownership metadata
- Lifecycle metadata
- Environment associations
- Governance metadata

---

## FR-006 Subscription Model

Subscription resources shall support:

- Parent topic relationship
- Filter metadata
- Rule metadata
- Consumer application relationships
- Delivery semantics metadata
- Dead-letter metadata
- Retry metadata
- Ownership metadata
- Lifecycle metadata
- Operational metadata

---

## FR-007 Contract Model

The platform shall support logical message contract definitions.

Contracts shall support:

- Semantic versioning
- Schema references
- Format classification
- Compatibility metadata
- Producer associations
- Consumer associations
- Deprecation status
- Example payloads
- Validation metadata
- External schema references

Supported schema styles may include:

- JSON Schema
- Avro
- Protobuf
- XML Schema
- CloudEvents
- Custom formats

---

## FR-008 Relationship Graph

The platform shall model explicit relationships between resources.

Relationships shall support:

- Directionality
- Relationship typing
- Metadata annotations
- Validation rules
- Traversal operations
- Search indexing

### Example Relationships

| Source | Relationship | Target |
|---|---|---|
| Producer App | publishes-to | Topic |
| Queue | uses-contract | Message Contract |
| Subscription | consumed-by | Consumer App |
| Team | owns | Queue |

---

## FR-009 Ownership Model

All first-class resources shall support ownership metadata.

Ownership metadata shall include:

- Owning team
- Technical contact
- Business contact
- Escalation metadata
- Support metadata
- Operational tier

Ownership shall support future Entra ID and Graph integrations.

---

## FR-010 Lifecycle States

Resources shall support standardized lifecycle states.

Minimum lifecycle states:

| State | Description |
|---|---|
| Draft | In-progress definition |
| Active | Operational and supported |
| Deprecated | Operational but scheduled for retirement |
| Retired | No longer operational |
| Archived | Historical metadata retained |

Lifecycle transitions must support validation rules.

---

## FR-011 Versioning Model

The platform shall support semantic versioning.

Versioning shall support:

- Major versions
- Minor versions
- Patch versions
- Compatibility indicators
- Current version references
- Deprecated version tracking
- Historical lineage

Versioning must support both:

- Resource-level versioning
- Contract-level versioning

---

## FR-012 Extensibility Model

The platform shall support custom metadata extensions.

Extensions shall:

- Avoid schema forks
- Support namespaced extension keys
- Support structured JSON payloads
- Support validation metadata
- Support indexing inclusion/exclusion

### Example

```json
{
  "extensions": {
    "contoso:costCenter": "FIN-102",
    "contoso:dataSensitivity": "confidential"
  }
}
```

---

## FR-013 Validation Model

The platform shall support validation rules for resources.

Validation shall support:

- Required fields
- Naming standards
- Relationship validation
- Duplicate detection
- Lifecycle validation
- Ownership validation
- Contract compatibility validation

Validation results shall be stored as structured metadata.

---

## FR-014 Searchability Requirements

The domain model shall support efficient search indexing.

All major entities shall support:

- Full-text search
- Faceted filtering
- Tag filtering
- Ownership filtering
- Environment filtering
- Relationship traversal
- Lifecycle filtering
- Contract filtering

---

## FR-015 Audit Metadata

All mutable resources shall support audit metadata.

Audit metadata shall include:

- Created by
- Created timestamp
- Modified by
- Modified timestamp
- Source system
- Synchronization metadata

---

## FR-016 Import and Export Compatibility

The platform shall support portable metadata serialization.

Serialization formats may include:

- JSON
- YAML
- OpenAPI extensions
- AsyncAPI-aligned exports

Exports must preserve:

- Relationships
- Identifiers
- Version metadata
- Ownership metadata
- Extensions

---

## FR-017 Environment Awareness

Resources shall support environment associations.

Minimum supported environment classifications:

- Development
- Test
- QA
- Staging
- Production
- Disaster Recovery

Environment metadata must not require duplicate logical resources.

---

## FR-018 Classification Metadata

Resources shall support classification metadata.

Examples include:

- Criticality
- Data sensitivity
- Compliance scope
- Availability tier
- Business domain
- Operational tier

---

## FR-019 Documentation Integration

Resources shall support linked documentation metadata.

Documentation links may include:

- Runbooks
- Wikis
- Architecture diagrams
- AsyncAPI specs
- Operational guides
- External repositories

---

## FR-020 Soft Delete and Retention

The platform shall support soft deletion semantics.

Deleted resources shall:

- Retain identifiers
- Retain audit history
- Retain relationship lineage
- Support restoration workflows

---

# Canonical Entity Model

## Base Resource Structure

```csharp
public abstract class Resource
{
    public Guid Id { get; init; }
    public string ResourceType { get; init; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }

    public LifecycleState LifecycleState { get; set; }
    public VersionInfo Version { get; set; }

    public OwnershipInfo Ownership { get; set; }
    public AuditInfo Audit { get; set; }

    public IReadOnlyCollection<TagReference> Tags { get; set; }
    public IReadOnlyDictionary<string, object> Extensions { get; set; }
}
```

This model is conceptual and not a persistence implementation.

---

# Domain Modeling Conventions

## Identifier Strategy

### Requirements

- Identifiers must be immutable
- Identifiers must be globally unique
- Identifiers must not embed environment names
- Identifiers must not depend on Azure resource IDs

### Preferred Approach

- GUID/UUID identifiers
- Stable logical names
- Optional human-readable slugs

---

## Naming Conventions

### Resource Naming

Resources shall support:

- Logical name
- Display name
- Fully qualified namespace path

### Constraints

- Lowercase preferred for logical names
- Hyphen-separated naming preferred
- Spaces disallowed in logical names
- Display names may contain spaces

---

## Relationship Modeling

Relationships must:

- Be explicit
- Be directional
- Support metadata
- Support traversal
- Support indexing

Relationships must not:

- Depend solely on inferred naming
- Require graph database adoption

---

# Storage Guidance

## Canonical Persistence

The canonical metadata store shall be:

- Azure Cosmos DB
- JSON document-oriented
- Optimized for flexible schema evolution

## Design Guidance

The storage model should:

- Preserve canonical resource documents
- Support relationship traversal
- Support indexing projections
- Support event-driven change propagation

## Explicitly Deferred

This specification intentionally defers:

- Final partition key strategy
- Physical container structure
- RU optimization
- Cross-region topology

These concerns belong to later implementation and operational specifications.

---

# API Alignment Requirements

The domain model must support:

- REST APIs
- Future GraphQL support
- Search projections
- AsyncAPI generation
- AI enrichment pipelines
- Import/export workflows

The domain model must remain API-agnostic.

---

# Future Evolution Considerations

The model should support future capabilities including:

- SaaS tenancy
- Federation
- Multi-cloud brokers
- Kafka registry support
- RabbitMQ registry support
- Event mesh visualization
- Dependency graph analysis
- Drift detection
- Automated governance enforcement
- AI-assisted metadata enrichment

The current implementation shall not prematurely optimize for these scenarios.

---

# Technical Constraints

## Infrastructure as Code Standard

All infrastructure provisioning, environment composition, deployment orchestration, and reusable infrastructure modules shall standardize exclusively on OpenTofu.

Bicep, ARM templates, and Terraform shall not be used as primary infrastructure authoring approaches within BusTerminal.

All infrastructure-oriented downstream specifications must align to OpenTofu module composition, state management, and environment promotion workflows.



## Technology Alignment

The implementation shall align with:

- .NET 10
- Modern C# patterns
- OpenTofu-managed infrastructure provisioning
- Azure Cosmos DB
- Azure AI Search
- OpenTelemetry-compatible metadata
- JSON-native persistence

---

# Security and Compliance Considerations

The domain model must support:

- Resource-level authorization metadata
- Classification metadata
- Audit traceability
- Ownership accountability
- Governance annotations

The model must not:

- Store secrets
- Store credentials
- Store connection strings
- Store message payload history

---

# Acceptance Criteria

## AC-001 Canonical Model Defined

All first-class BusTerminal resource types are defined with canonical metadata structures.

## AC-002 Relationship Model Implemented

Relationships between resources are explicit and traversable.

## AC-003 Lifecycle Semantics Established

All resources support standardized lifecycle states.

## AC-004 Extensibility Supported

Organizations can add metadata without modifying the canonical schema.

## AC-005 Versioning Semantics Defined

Version lineage and compatibility semantics are clearly modeled.

## AC-006 Ownership Model Implemented

All operational resources support structured ownership metadata.

## AC-007 Validation Standards Established

Validation rules and validation result structures are defined.

## AC-008 Searchability Supported

The domain model supports rich search indexing and faceted discovery.

## AC-009 Environment Awareness Implemented

Resources support environment classification without requiring duplication.

## AC-010 Storage Independence Preserved

The canonical domain model remains independent from persistence implementation details.

---

# Dependencies

## Upstream Dependencies

- 001-brand-system-and-design-foundation
- 002-solution-foundation
- 003-auth-and-identity

## Downstream Consumers

- API specifications
- Search specifications
- UI resource explorer
- Governance engine
- Import/export engine
- AI enrichment services
- Visualization components
- Relationship graph tooling

---

# Implementation Notes

## Recommended Development Order

1. Base resource abstractions
2. Namespace model
3. Queue/topic/subscription entities
4. Relationship model
5. Ownership model
6. Versioning model
7. Validation framework
8. Extensibility model
9. Serialization/export support
10. Search projection alignment

---

# Risks

| Risk | Mitigation |
|---|---|
| Schema instability | Favor additive evolution |
| Over-modeling | Keep runtime concerns separate |
| Tight Azure coupling | Maintain logical abstractions |
| Relationship complexity | Standardize relationship patterns |
| Future extensibility challenges | Use namespaced extension model |

---

# Open Questions

## OQ-001

Should future federation scenarios support cross-registry relationship references?

## OQ-002

Should contract compatibility validation become pluggable?

## OQ-003

Should AsyncAPI become a first-class internal storage format or remain export-oriented?

## OQ-004

Should relationship traversal eventually support graph-native storage projections?

---

# Deliverables

This specification shall produce:

- Canonical domain entity definitions
- Relationship model definitions
- Lifecycle model definitions
- Validation conventions
- Versioning conventions
- Extensibility conventions
- Serialization conventions
- Storage alignment guidance
- Search indexing alignment guidance

