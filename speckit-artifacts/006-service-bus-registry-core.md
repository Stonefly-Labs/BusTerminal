# 006-service-bus-registry-core

## Overview

Implement the foundational Service Bus Registry capabilities for BusTerminal.

This spec establishes the core registry domain, APIs, synchronization primitives, indexing pipeline, and frontend experiences required to register, browse, search, and manage Azure Service Bus assets across environments.

This is the first feature-complete product slice that transforms BusTerminal from a platform shell into a usable registry application.

---

# Goals

## Primary Goals

- Create a canonical registry model for Azure Service Bus assets
- Enable CRUD operations for registry entities
- Support relationships between messaging resources
- Persist registry metadata in Cosmos DB
- Index registry content into Azure AI Search
- Provide a modern searchable UI for discovery and navigation
- Establish synchronization and audit foundations
- Support environment-aware registry management

---

# Non-Goals

The following capabilities are explicitly excluded from this spec:

- Automatic Azure discovery/synchronization
- AI-assisted semantic search
- Documentation generation
- Ownership governance workflows
- CLI tooling
- Deep operational telemetry
- Advanced RBAC beyond existing auth foundations
- Multi-cloud support
- Multi-tenant partitioning

These capabilities will be implemented in future specs.

---

# Architecture Alignment

This spec must conform to the BusTerminal Constitution.

Required architectural standards:

- Backend: .NET 10 + ASP.NET Core
- Frontend: NextJS 16 App Router
- UI Components: shadcn/ui
- Styling: Tailwind CSS
- Infrastructure: OpenTofu only
- Azure-first implementation strategy
- Managed identity authentication wherever possible
- Accessibility-compliant UI
- Strongly typed contracts across frontend/backend boundaries

---

# Functional Requirements

## Registry Entity Types

The system shall support the following registry entity types:

### Namespace
Represents an Azure Service Bus namespace.

### Queue
Represents a queue within a namespace.

### Topic
Represents a topic within a namespace.

### Subscription
Represents a topic subscription.

### Rule
Represents a subscription rule/filter.

---

# Canonical Registry Fields

All registry entities must support the following shared fields:

| Field | Description |
|---|---|
| id | Stable GUID identifier |
| entityType | Namespace/Queue/Topic/etc |
| name | Logical resource name |
| fullyQualifiedName | Canonical full path |
| description | Optional description |
| tags | Arbitrary metadata tags |
| owner | Owning team/person |
| environment | Dev/Test/Prod/etc |
| status | Active/Deprecated/Deleted |
| createdAtUtc | Creation timestamp |
| updatedAtUtc | Last update timestamp |
| source | Manual/Discovered |
| azureResourceId | ARM resource identifier |
| namespaceName | Parent namespace |
| metadata | Extensible JSON metadata |

---

# Entity Relationships

The system shall support relationship modeling between entities.

Examples:

- Namespace -> Queue
- Namespace -> Topic
- Topic -> Subscription
- Subscription -> Rule

Relationships must support:

- Parent/child traversal
- Dependency graph rendering
- Efficient lookup queries

---

# Backend Requirements

## API Architecture

Implement REST APIs using ASP.NET Core Minimal APIs or Controllers.

Preferred structure:

/src
  /BusTerminal.Api
  /BusTerminal.Application
  /BusTerminal.Domain
  /BusTerminal.Infrastructure

---

## API Endpoints

### Registry CRUD

| Method | Route |
|---|---|
| GET | /api/registry |
| GET | /api/registry/{id} |
| POST | /api/registry |
| PUT | /api/registry/{id} |
| DELETE | /api/registry/{id} |

---

## Query Features

Support:

- Pagination
- Sorting
- Filtering
- Environment filtering
- Tag filtering
- Entity type filtering
- Full-text search integration

---

## Validation

Implement validation rules for:

- Duplicate resource names
- Invalid entity relationships
- Missing required metadata
- Invalid environment classifications
- Invalid Azure resource IDs

Validation should use FluentValidation.

---

## Domain Modeling

Use strongly typed domain models.

Avoid anemic models where possible.

Prefer:

- Value objects
- Enumerations
- Domain invariants
- Immutable patterns where appropriate

---

# Cosmos DB Requirements

## Persistence Model

Use Cosmos DB as the source of truth.

Preferred container strategy:

| Container | Purpose |
|---|---|
| registry-entities | Primary registry data |
| registry-relationships | Relationship graph |
| registry-audit | Audit trail events |

---

## Partitioning Strategy

Partition key:

/environment

Rationale:

- Single-tenant architecture
- Environment-level query isolation
- Future scalability flexibility

---

## Data Access

Implement repository abstractions.

Requirements:

- Async-first
- Cancellation token support
- Optimistic concurrency
- Structured diagnostics logging

---

# Azure AI Search Requirements

## Search Index

Create searchable indexes for registry entities.

Searchable fields:

- name
- description
- tags
- owner
- fullyQualifiedName
- metadata

Filterable fields:

- entityType
- environment
- status

Sortable fields:

- name
- updatedAtUtc

---

## Search Pipeline

Implement indexing pipeline triggered on:

- Create
- Update
- Delete

Indexing must be resilient and retryable.

---

# Frontend Requirements

## Frontend Stack

Required technologies:

- NextJS 16
- App Router
- TypeScript strict mode
- shadcn/ui
- Tailwind CSS
- TanStack Query
- Zod
- React Hook Form

---

# Required UI Features

## Registry Explorer

Implement a registry browsing experience with:

- Tree navigation
- Expand/collapse hierarchy
- Entity icons
- Environment indicators
- Search integration

---

## Entity Detail Pages

Each entity detail page must display:

- Metadata
- Relationships
- Tags
- Ownership
- Audit information placeholder
- Environment
- Resource identifiers

---

## Registry Search

Implement global search UI supporting:

- Full text search
- Entity type filters
- Environment filters
- Tag filters

---

## Create/Edit Experiences

Implement forms for:

- Queue registration
- Topic registration
- Subscription registration
- Metadata editing

Use Zod validation shared with backend DTO contracts where practical.

---

# UX Requirements

The UI must feel:

- Modern
- Fast
- Minimal
- Enterprise-grade
- Dense but readable

Prioritize:

- Keyboard navigation
- Accessibility
- Low-friction workflows
- Responsive layouts

---

# Design System Alignment

Must conform to Spec 001 branding/design standards.

Requirements:

- Reusable primitives
- Theme-aware components
- Consistent spacing
- Consistent typography
- Accessible color contrast

---

# Security Requirements

Use existing authentication foundations from Spec 003.

Requirements:

- Entra ID authentication
- Managed identity usage
- Secure API authorization
- No secrets in code
- Key Vault integration for configuration

---

# Infrastructure Requirements

Provision required Azure resources using OpenTofu.

Required resources:

- Cosmos DB
- Azure AI Search
- Azure Container Apps
- Log Analytics
- Application Insights
- Managed identities

---

# OpenTofu Requirements

Infrastructure code organization:

/infrastructure
  /modules
  /environments

Requirements:

- Prefer Azure Verified Modules where possible
- Environment parameterization
- Remote state support
- CI/CD compatibility

---

# Observability Requirements

Implement:

- Structured logging
- Distributed tracing
- Health endpoints
- Correlation IDs
- Failure diagnostics

Application Insights integration is required.

---

# Audit Foundations

Implement basic audit event recording for:

- Create
- Update
- Delete

Audit records must include:

- User identity
- Timestamp
- Entity type
- Entity ID
- Change summary

---

# Testing Requirements

## Backend

Required:

- Unit tests
- Integration tests
- Cosmos repository tests
- API contract tests

---

## Frontend

Required:

- Component tests
- Form validation tests
- Query state tests

---

# Performance Requirements

Registry search and navigation should feel responsive.

Targets:

| Capability | Target |
|---|---|
| Registry search | < 1 second |
| Entity detail load | < 500ms |
| CRUD operations | < 1 second |

---

# Accessibility Requirements

Must meet WCAG AA standards.

Requirements:

- Keyboard accessibility
- Proper semantic markup
- Screen reader compatibility
- Focus visibility
- Accessible forms

---

# Deliverables

## Backend Deliverables

- Registry domain model
- CRUD APIs
- Validation pipeline
- Cosmos persistence layer
- AI Search indexing layer
- Relationship model
- Audit recording

---

## Frontend Deliverables

- Registry explorer UI
- Entity detail pages
- Search UI
- CRUD forms
- Filter experiences

---

## Infrastructure Deliverables

- OpenTofu infrastructure
- Cosmos provisioning
- AI Search provisioning
- Managed identities
- Container Apps deployment

---

# Acceptance Criteria

## Functional Acceptance

- Users can create registry entities
- Users can edit registry entities
- Users can delete registry entities
- Users can search registry entities
- Users can browse relationships
- Registry entities persist correctly
- Search indexes update correctly

---

## Technical Acceptance

- Builds succeed in CI
- Infrastructure deploys successfully
- APIs are authenticated
- Logging/tracing operational
- Search indexing operational
- Tests passing

---

# Implementation Guidance

Recommended execution order:

1. Domain models
2. Cosmos persistence
3. CRUD APIs
4. Validation
5. Search indexing
6. Frontend explorer
7. Search UI
8. CRUD forms
9. Relationship rendering
10. Audit recording
11. Observability hardening

---

# Suggested Future Extensions

Future specs may extend this foundation with:

- Automatic Azure discovery
- Drift detection
- Semantic search
- AI copilots
- Governance workflows
- Operational analytics
- CLI tooling
- Event replay analysis

---
