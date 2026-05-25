# Feature Specification: 005-infrastructure-baseline

**Feature Branch**: `005-infrastructure-baseline`  
**Created**: 2026-05-14  
**Status**: Draft  
**Input**: Provision Azure infrastructure using OpenTofu including Container Apps, Cosmos DB, Azure AI Search, Key Vault, Service Bus, observability resources, networking, and managed identities.

---

## User Scenarios & Testing

### Primary User Story

As a BusTerminal operator or contributor, I need a repeatable OpenTofu-based infrastructure baseline so that the application can be deployed consistently into an Azure tenant with secure-by-default networking, managed identities, observability, and all required platform dependencies.

### Acceptance Scenarios

1. **Given** a target Azure subscription and environment configuration, **when** the infrastructure deployment is applied, **then** all required baseline Azure resources are provisioned successfully.
2. **Given** the application is deployed to Azure Container Apps, **when** backend services access Cosmos DB, Key Vault, Azure AI Search, and Service Bus, **then** access uses managed identity and least-privilege role assignments.
3. **Given** platform resources are deployed, **when** an operator reviews network exposure, **then** sensitive data services are not broadly exposed by default and support private networking patterns.
4. **Given** infrastructure has been deployed, **when** an operator reviews logs and metrics, **then** Container Apps, application telemetry, dependency telemetry, and platform diagnostics are routed to centralized observability resources.
5. **Given** a contributor modifies infrastructure code, **when** validation runs, **then** OpenTofu formatting, validation, planning, and policy/security checks complete before changes are accepted.

### Edge Cases

- Deployment must support clean creation in a new resource group.
- Deployment must support repeatable updates without destructive replacement of stateful resources unless explicitly requested.
- Resource names must remain globally unique where Azure requires uniqueness.
- Environments must be independently deployable without shared mutable state.
- A failed deployment must leave infrastructure in a recoverable state.
- Local development and cloud-hosted environments must use the same resource model where practical.

---

## Requirements

### Functional Requirements

- **FR-001**: The solution MUST define Azure infrastructure using OpenTofu only.
- **FR-002**: The solution MUST support environment-specific configuration for at least `dev`, `test`, and `prod`.
- **FR-003**: The solution MUST provision Azure Container Apps resources for the frontend and backend application workloads.
- **FR-004**: The solution MUST provision or reference an Azure Container Apps Environment suitable for running the application workloads.
- **FR-005**: The solution MUST provision Cosmos DB as the metadata store.
- **FR-006**: The solution MUST provision Azure AI Search for searchable registry metadata and discovery scenarios.
- **FR-007**: The solution MUST provision Azure Key Vault for application secrets, certificates, and sensitive configuration.
- **FR-008**: The solution MUST provision Azure Service Bus resources required by BusTerminal internal messaging workflows.
- **FR-009**: The solution MUST provision centralized observability resources, including Log Analytics and Application Insights or equivalent workspace-based telemetry configuration.
- **FR-010**: The solution MUST provision managed identities for application workloads and automation components.
- **FR-011**: The solution MUST assign least-privilege Azure RBAC permissions to managed identities for Cosmos DB, Key Vault, Azure AI Search, Service Bus, and observability access.
- **FR-012**: The solution MUST support private networking patterns for data services and platform dependencies where supported and appropriate.
- **FR-013**: The solution MUST define virtual network, subnet, DNS, and private endpoint conventions needed for the baseline deployment.
- **FR-014**: The solution MUST emit key deployment outputs required by later specs, including endpoint URLs, managed identity principal IDs, resource IDs, and configuration references.
- **FR-015**: The solution MUST use consistent tagging across all supported resources.
- **FR-016**: The solution MUST support diagnostic settings for supported Azure resources.
- **FR-017**: The solution MUST define state management expectations for OpenTofu, including remote state backend conventions.
- **FR-018**: The solution MUST provide validation commands and CI/CD integration expectations for infrastructure changes.
- **FR-019**: The solution MUST avoid storing secrets in OpenTofu variables, state outputs, repository files, or CI/CD logs.
- **FR-020**: The solution MUST document production hardening switches separately from local or development conveniences.

### Non-Functional Requirements

- **NFR-001 Security**: Infrastructure must default to least privilege, managed identity, encrypted services, and minimal public exposure.
- **NFR-002 Reliability**: Stateful services must use SKUs and configuration patterns appropriate for the selected environment tier.
- **NFR-003 Cost Awareness**: Development deployments must support lower-cost SKUs while preserving production-compatible topology.
- **NFR-004 Operability**: Operators must be able to inspect health, logs, metrics, traces, and dependency failures from a central observability workspace.
- **NFR-005 Portability**: The infrastructure code must be reusable across Azure subscriptions and regions with environment-specific parameterization.
- **NFR-006 Maintainability**: OpenTofu modules must be organized by capability boundaries and avoid excessive coupling between unrelated platform components.

---

## Key Entities

- **Environment**: A named deployment target such as `dev`, `test`, or `prod`, with its own configuration, state, and resource naming scope.
- **Resource Group**: The Azure container for environment-specific platform resources.
- **Container Apps Environment**: The hosting boundary for frontend and backend container apps.
- **Frontend Container App**: The Next.js web application runtime.
- **Backend Container App**: The .NET API/runtime service for BusTerminal.
- **Cosmos DB Account**: Metadata persistence store for registry records, ownership, classification, lifecycle, and related domain data.
- **Azure AI Search Service**: Search and discovery index service for registry metadata.
- **Key Vault**: Secure store for secrets, certificates, and sensitive operational settings.
- **Service Bus Namespace**: Messaging namespace for asynchronous workflows and internal events.
- **Managed Identity**: Identity assigned to workloads and automation for Azure resource access.
- **Virtual Network**: Network boundary for private endpoints, integration subnets, and secure resource access.
- **Private Endpoint**: Private connectivity mechanism for supported Azure PaaS services.
- **Private DNS Zone**: DNS mapping used by private endpoints.
- **Observability Workspace**: Central telemetry destination for logs, metrics, traces, and diagnostics.

---

## Infrastructure Scope

### In Scope

- Resource group and naming baseline.
- OpenTofu module structure and environment layout.
- Azure Container Apps hosting baseline.
- Cosmos DB baseline.
- Azure AI Search baseline.
- Key Vault baseline.
- Service Bus namespace and baseline queues/topics needed by the app foundation.
- Managed identities and RBAC assignments.
- Virtual network, subnets, private endpoints, and private DNS zones.
- Log Analytics, Application Insights, diagnostic settings, and telemetry configuration.
- OpenTofu state backend conventions.
- CI/CD validation and plan/apply conventions.
- Required outputs for application deployment and later specs.

### Out of Scope

- Application business logic.
- Registry domain model design beyond infrastructure needs.
- Search index schema implementation beyond service provisioning and configuration outputs.
- Detailed frontend design system implementation.
- End-user authentication flows, except infrastructure dependencies required for identity integration.
- SaaS multi-tenant hosting architecture.
- Kubernetes or AKS hosting.
- Bicep, ARM template, or Pulumi implementations.

---

## OpenTofu Design Requirements

### Repository Layout

The infrastructure implementation SHOULD use a structure similar to:

```text
infra/
  opentofu/
    environments/
      dev/
        main.tf
        variables.tf
        outputs.tf
        terraform.tfvars.example
      test/
        main.tf
        variables.tf
        outputs.tf
        terraform.tfvars.example
      prod/
        main.tf
        variables.tf
        outputs.tf
        terraform.tfvars.example
    modules/
      naming/
      resource-group/
      networking/
      container-apps/
      cosmos-db/
      ai-search/
      key-vault/
      service-bus/
      observability/
      managed-identity/
      role-assignments/
    policies/
    scripts/
```

### Module Expectations

- Modules MUST expose clear inputs and outputs.
- Modules MUST avoid hardcoded subscription IDs, tenant IDs, resource group names, or region names.
- Modules MUST support tagging.
- Modules MUST expose resource IDs needed by dependent modules.
- Modules MUST avoid circular dependencies.
- Modules SHOULD separate stateful services from compute/runtime modules where practical.

### State Management

- OpenTofu state MUST be remote for shared environments.
- State storage MUST be environment-scoped.
- State storage MUST be protected from accidental deletion where practical.
- Secrets MUST NOT be written to outputs.
- Sensitive variables MUST be marked sensitive.
- State access MUST be limited to deployment identities and authorized maintainers.

---

## Azure Resource Requirements

### Container Apps

- Provision an Azure Container Apps Environment.
- Provision frontend and backend Container Apps or provide module hooks for app deployment specs to create them.
- Support user-assigned managed identities for workloads.
- Support ingress configuration appropriate to each app:
  - Frontend: external ingress may be enabled depending on environment.
  - Backend: internal ingress preferred unless external API exposure is explicitly required.
- Support environment variables sourced from non-secret outputs and Key Vault references where applicable.
- Configure workload telemetry integration with observability resources.

### Cosmos DB

- Provision a Cosmos DB account for metadata persistence.
- Use environment-appropriate capacity configuration.
- Enable backup and consistency settings appropriate to the environment.
- Prefer identity-based access where supported by the application stack.
- Support private endpoint configuration.
- Emit database/account connection metadata required by application configuration without exposing secrets.

### Azure AI Search

- Provision an Azure AI Search service.
- Support environment-specific SKU selection.
- Support managed identity access patterns where available.
- Support private networking configuration where appropriate.
- Emit endpoint and resource ID outputs for backend configuration.

### Key Vault

- Provision Key Vault with RBAC authorization preferred over access policies unless a technical constraint requires otherwise.
- Enable purge protection for production.
- Enable soft delete.
- Support private endpoint configuration.
- Assign least-privilege workload access.
- Do not provision long-lived application secrets unless required by a later spec.

### Service Bus

- Provision a Service Bus namespace.
- Define baseline queues/topics only where known infrastructure-level needs exist.
- Use managed identity and Azure RBAC for application access.
- Support private endpoint configuration where appropriate.
- Emit namespace and entity identifiers required by the backend.

### Observability

- Provision Log Analytics.
- Provision workspace-based Application Insights or equivalent telemetry binding.
- Configure diagnostic settings for supported platform resources.
- Define retention settings by environment.
- Emit instrumentation/configuration outputs needed by application runtime.

### Networking

- Provision virtual network and subnets required for Container Apps and private endpoints.
- Define private DNS zones for supported private endpoint services.
- Associate private DNS zones with the virtual network.
- Avoid unnecessary public network exposure in production.
- Make public access switches explicit and environment-scoped.

### Managed Identity and RBAC

- Provision user-assigned managed identities for frontend, backend, and deployment automation where appropriate.
- Assign least-privilege role assignments.
- Scope role assignments as narrowly as possible.
- Avoid broad subscription-level permissions unless explicitly required for deployment automation.

---

## Configuration Model

Environment configuration MUST include, at minimum:

- Environment name.
- Azure region.
- Resource name prefix/suffix conventions.
- Tags.
- SKU choices by service.
- Network CIDR ranges.
- Public access toggles.
- Private endpoint toggles.
- Log retention settings.
- Container image placeholders or references.
- Principal IDs or object IDs needed for deployment-time access.

---

## Security Requirements

- No secrets in source control.
- No secrets in OpenTofu outputs.
- No shared application credentials when managed identity is available.
- Production Key Vault must enable purge protection.
- Production state storage must have restricted access.
- Diagnostic logging must not intentionally capture sensitive payloads.
- Public network access must be explicitly justified for production services.
- RBAC assignments must be documented and reviewed.

---

## Observability Requirements

- Container Apps logs must route to Log Analytics.
- Application telemetry must correlate frontend, backend, and dependency calls where practical.
- Platform diagnostic logs must be enabled for critical services.
- Metrics must support operational dashboards in later specs.
- Infrastructure outputs must include observability identifiers needed by application configuration.

---

## Validation & Quality Gates

Infrastructure changes MUST pass:

- `tofu fmt`.
- `tofu validate`.
- Environment-specific `tofu plan`.
- Static security scanning for OpenTofu configuration.
- Policy checks for public exposure, missing diagnostics, missing tags, and excessive RBAC.
- Review of destructive changes before apply.

---

## Deliverables

- OpenTofu environment layout.
- Reusable OpenTofu modules.
- Example environment variable files or tfvars templates.
- Remote state configuration guidance.
- CI/CD validation workflow guidance.
- Resource naming and tagging conventions.
- Required outputs for application deployment.
- Documentation for local, dev, test, and prod deployment differences.

---

## Success Criteria

- A contributor can deploy the baseline infrastructure into a clean Azure subscription/resource group using documented OpenTofu commands.
- All required baseline services are provisioned and connected through outputs.
- Application workloads can authenticate to required Azure services using managed identity.
- Production deployment defaults avoid unnecessary public exposure.
- Observability resources receive platform diagnostics and application telemetry configuration.
- The infrastructure code can be validated and planned automatically in CI/CD.

---

## Assumptions

- BusTerminal is deployed as a single-tenant open-source solution into an organization-owned Azure tenant.
- The solution does not model SaaS tenant boundaries in this spec.
- Application runtime will use .NET 10 for backend services and Next.js 16.x for frontend services.
- Azure Container Apps is the target application hosting platform.
- OpenTofu is the only infrastructure-as-code tool used for this baseline.
- Private networking is a production expectation, while development environments may permit selected public access for cost or simplicity when explicitly configured.

---

## Dependencies

- `001-brand-system-and-design-foundation` for frontend design assumptions, but not required for infrastructure provisioning.
- `002-solution-foundation` for application shell, CI/CD conventions, health checks, and runtime structure.
- `003-auth-and-identity` for identity conventions and authorization boundaries.
- `004-core-domain-model` for eventual Cosmos DB data model alignment.

---

## Open Questions

- Should the baseline provision Azure Container Registry, or is image publishing handled by `002-solution-foundation`?
- Should production enforce fully internal backend ingress from the first implementation slice?
- Which Azure AI Search SKU should be the default for development?
- Should Cosmos DB use serverless for development and provisioned throughput for production?
- Should Service Bus start with a namespace only, or include baseline topics for domain events?
- Should remote state storage be created by a bootstrap step or managed outside this spec?
