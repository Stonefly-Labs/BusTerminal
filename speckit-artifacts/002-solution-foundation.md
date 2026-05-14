# 002-solution-foundation

## Purpose

Establish the deployable application foundation for BusTerminal including:

- Frontend and backend application structure
- Azure Container Apps hosting model
- OpenID Connect and Entra ID authentication plumbing
- Observability and diagnostics
- Health monitoring
- CI/CD pipelines
- Baseline Azure infrastructure
- Local development standards
- Environment promotion patterns

This slice establishes the operational and deployment baseline all future slices build upon.

---

# Goals

## Primary Goals

- Create a production-ready monorepo structure
- Establish a consistent frontend/backend architecture
- Enable local and cloud execution
- Deploy workloads to Azure Container Apps
- Implement secure identity foundations
- Implement centralized logging and telemetry
- Enable repeatable environment deployments using OpenTofu
- Establish CI/CD standards
- Implement operational readiness patterns

## Non-Goals

This slice does NOT implement:

- Domain-specific business logic
- Registry ingestion
- AI capabilities
- Advanced RBAC enforcement
- Production-scale optimization
- Multi-region failover
- Tenant isolation
- Complex workflow orchestration

---

# Architectural Principles

## Cloud-Native First

The solution MUST embrace Azure-native operational patterns while maintaining reasonable portability boundaries at the application layer.

## OpenTofu as the IaC Standard

All infrastructure provisioning MUST use OpenTofu.

The project MUST NOT introduce:
- Bicep
- ARM templates
- Terraform Cloud dependencies
- Mixed IaC technologies

OpenTofu modules MUST be:
- Environment composable
- Idempotent
- CI/CD friendly
- Remote-state capable
- Structured for future reusable modules

## Containerized Everything

All deployable services MUST run as containers.

No workload may depend on:
- IIS
- VM-hosted deployment models
- Azure App Service-specific runtime assumptions

## Secure by Default

Identity, secrets, networking, and observability MUST be established before feature development begins.

## Developer Experience Matters

The foundation MUST optimize for:
- Agentic development workflows
- Fast local iteration
- Predictable builds
- Minimal onboarding friction
- Reproducible environments

---

# Required Technology Stack

## Frontend

- Next.js 16.x
- React 19+
- TypeScript
- Tailwind CSS
- shadcn/ui
- TanStack Query
- Zod
- React Hook Form
- Lucide icons

## Backend

- .NET 10
- ASP.NET Core Minimal APIs
- C#
- OpenAPI support
- Serilog
- Aspire-compatible service defaults where useful

## Infrastructure

- Azure Container Apps
- Azure Container Registry
- Azure Log Analytics
- Azure Application Insights
- Azure Key Vault
- Azure Managed Identities
- Azure Monitor
- Azure Container Apps Environment

## Infrastructure as Code

- OpenTofu ONLY
- Modular OpenTofu layout
- AzureRM provider
- Remote state support

## CI/CD

- GitHub Actions
- Environment-based deployments
- OIDC federation to Azure
- Container image promotion model

---

# Repository Structure

```text
/src
  /frontend
  /backend
  /shared

/infrastructure
  /opentofu
    /modules
    /environments
      /dev
      /test
      /prod

/.github
  /workflows

/docs

/scripts
```

---

# Frontend Requirements

## Application Standards

The frontend MUST:

- Use App Router
- Use server components by default
- Use client components only when necessary
- Support responsive layouts
- Support dark mode
- Implement accessibility-first patterns
- Support future PWA enablement

## API Integration

Frontend services MUST:

- Use typed API clients
- Centralize API configuration
- Support token propagation
- Implement resilient retry behavior where appropriate

## UI Standards

The frontend MUST use:

- shadcn/ui as the primary component system
- Tailwind CSS utilities
- Centralized design tokens
- Accessible primitives

Custom UI frameworks are prohibited.

---

# Backend Requirements

## API Architecture

The backend MUST:

- Use Minimal APIs
- Group endpoints by feature area
- Use endpoint versioning conventions
- Generate OpenAPI specifications
- Support structured validation

## Dependency Injection

The backend MUST:

- Use Microsoft.Extensions.DependencyInjection
- Register services via extension methods
- Separate infrastructure and application concerns

## Configuration

The backend MUST support:

- Local configuration
- Environment variables
- Managed identity
- Key Vault references

Secrets MUST NOT exist in source control.

---

# Authentication and Identity

## Authentication Provider

The platform MUST use:
- Microsoft Entra ID

## Authentication Flows

The foundation MUST support:
- Interactive user authentication
- API bearer token validation
- Managed identity authentication
- Future service-to-service authentication

## Managed Identity

Container Apps workloads MUST use managed identities for:

- Key Vault access
- Azure Monitor access
- Future Cosmos DB access
- Future Azure AI Search access

Connection strings SHOULD be avoided whenever possible.

---

# Azure Container Apps Standards

## Hosting Model

The platform MUST deploy:

- Frontend container app
- Backend API container app

## Ingress

The solution MUST support:
- External ingress for frontend
- Controlled ingress for APIs

## Scaling

The initial deployment MUST:
- Support scale-to-zero where appropriate
- Define min/max replicas
- Support HTTP scaling

## Health Checks

All services MUST expose:

- Liveness endpoint
- Readiness endpoint
- Startup health endpoint

---

# Observability Standards

## Logging

The platform MUST implement:

- Structured logging
- Correlation IDs
- Request tracing
- Centralized log aggregation

## Telemetry

The solution MUST integrate with:

- Application Insights
- Log Analytics
- Azure Monitor

## Distributed Tracing

Tracing MUST propagate:
- Request IDs
- Correlation IDs
- Activity context

Across frontend/backend boundaries.

---

# CI/CD Requirements

## GitHub Actions

Pipelines MUST include:

- Build validation
- Unit testing
- Container builds
- Security scanning
- OpenTofu validation
- OpenTofu plan
- OpenTofu apply
- Environment promotion

## Authentication

GitHub Actions MUST authenticate to Azure using:
- OIDC federation

Static credentials are prohibited.

## Deployment Strategy

The solution SHOULD support:

- Progressive rollout patterns
- Environment approvals
- Rollback capability

---

# OpenTofu Requirements

## State Management

OpenTofu state MUST support:
- Remote state
- State locking
- Environment isolation

## Module Structure

Modules SHOULD include:
- Networking
- Container Apps
- Identity
- Monitoring
- Shared platform resources

## Environment Structure

Separate environments MUST exist for:
- Development
- Test
- Production

Environment-specific values MUST NOT be hardcoded.

---

# Local Development Standards

## Required Tooling

Developers MUST be able to run the solution using:

- Docker Desktop
- Node.js LTS
- .NET 10 SDK
- OpenTofu CLI
- Azure CLI

## Local Execution

The platform MUST support:

- Local frontend execution
- Local backend execution
- Local container execution
- Mocked or development Azure dependencies

## Developer Scripts

Scripts SHOULD exist for:

- Bootstrap/setup
- Local startup
- Test execution
- Linting
- Formatting
- Infrastructure validation

---

# Security Standards

## Secret Management

Secrets MUST use:
- Azure Key Vault
- Environment variable injection

Secrets MUST NOT:
- Exist in git
- Exist in container images
- Exist in OpenTofu variable defaults

## Supply Chain Security

The platform SHOULD include:
- Dependency scanning
- Container scanning
- SBOM generation

---

# Deliverables

This slice MUST produce:

- Deployable frontend shell
- Deployable backend shell
- OpenTofu infrastructure baseline
- CI/CD pipelines
- Container build pipelines
- Logging baseline
- Authentication plumbing
- Health monitoring
- Local development documentation
- Environment deployment documentation

---

# Acceptance Criteria

The slice is complete when:

- Frontend deploys successfully to Azure Container Apps
- Backend deploys successfully to Azure Container Apps
- GitHub Actions fully deploy the environment
- OpenTofu provisions all required Azure resources
- Authentication flow works end-to-end
- Logs appear in Application Insights
- Health endpoints function correctly
- Local developer onboarding succeeds using documented steps
- No secrets exist in source control
- Managed identities authenticate successfully

---

# Future Considerations

The foundation SHOULD anticipate future support for:

- Cosmos DB integration
- Azure AI Search integration
- Service Bus integration
- Background workers
- Event-driven processing
- gRPC services
- Internal APIs
- API gateway introduction
- Multi-environment promotion
- Blue/green deployments
