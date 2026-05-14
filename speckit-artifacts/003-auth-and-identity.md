# 003-auth-and-identity

## Overview

This specification defines the authentication, identity, authorization, and workload identity foundation for BusTerminal. It establishes standardized Entra ID integration patterns across frontend, backend, infrastructure, Azure services, and future extensibility points.

The goal of this slice is to ensure BusTerminal implements secure-by-default identity practices from the beginning, avoiding future retrofits around authorization, workload identity, RBAC boundaries, and API trust models.

This specification intentionally establishes:
- Human identity architecture
- Service/workload identity architecture
- Authorization boundaries
- API authentication standards
- Graph integration foundations
- Token handling standards
- Managed identity conventions
- Tenant trust assumptions
- Security posture requirements

---

# Goals

## Primary Goals

- Implement modern Entra ID authentication for frontend and backend
- Eliminate static credentials wherever possible
- Standardize managed identity usage across Azure resources
- Establish authorization conventions and role boundaries
- Enable future Graph API integrations
- Support local development authentication patterns
- Provide secure service-to-service trust architecture
- Define token validation and claims standards
- Create reusable authentication middleware and libraries

## Secondary Goals

- Support future SaaS evolution without architectural rewrites
- Enable future CLI authentication support
- Support future delegated Graph access
- Enable future automation and provisioning capabilities
- Ensure compatibility with Azure Container Apps and Kubernetes

---

# Architectural Principles

## Identity-First Architecture

All trust relationships MUST originate from Entra ID identities or federated identities.

## Managed Identity by Default

All Azure-hosted workloads MUST authenticate using managed identities whenever Azure-native services support them.

## Least Privilege

Authorization MUST default to least privilege.

## Zero Trust Internal APIs

Internal APIs MUST validate tokens even inside private networking boundaries.

---

# Authentication Architecture

## Identity Provider

Primary identity provider:
- Microsoft Entra ID

Supported account types:
- Organizational Microsoft accounts only

Unsupported in MVP:
- Personal Microsoft accounts
- Social identity providers
- Local user databases
- Username/password auth

---

# Frontend Authentication

## Frontend Stack

Frontend:
- NextJS 16.x
- App Router
- TypeScript

Frontend authentication library:
- Microsoft Authentication Library (MSAL)

Preferred packages:
- `@azure/msal-browser`
- `@azure/msal-react`

## Authentication Flow

Required flow:
- Authorization Code Flow with PKCE

Implicit flow MUST NOT be used.

---

# Backend Authentication

## Backend Stack

Backend:
- .NET 10
- ASP.NET Core Minimal APIs

Authentication middleware:
- Microsoft Identity Web

Preferred packages:
- `Microsoft.Identity.Web`
- `Microsoft.AspNetCore.Authentication.JwtBearer`

---

# Authorization Model

## Initial Platform Roles

| Role | Purpose |
|---|---|
| BusTerminal.Admin | Full administrative access |
| BusTerminal.Operator | Operational management access |
| BusTerminal.Reader | Read-only access |
| BusTerminal.Developer | API/spec/developer tooling access |

---

# Managed Identity Standards

## Required Managed Identity Usage

All Azure-hosted workloads MUST use managed identities for:
- Azure service authentication
- Internal automation
- Background processing
- Infrastructure provisioning access

Preferred:
- User-assigned managed identities

Allowed:
- System-assigned managed identities

---

# Azure Resource Authentication Matrix

| Resource | Authentication Method |
|---|---|
| Cosmos DB | Managed Identity |
| Azure AI Search | Managed Identity |
| Key Vault | Managed Identity |
| Azure Storage | Managed Identity |
| Azure OpenAI | Managed Identity |
| Azure Service Bus | Managed Identity |
| App Configuration | Managed Identity |
| Log Analytics | Entra ID |
| Application Insights | Entra ID |

---

# Local Development Authentication

## Developer Authentication

Local development SHOULD use:
- Azure CLI authentication
- Visual Studio authentication
- VS Code authentication
- Developer login via Entra ID

## DefaultAzureCredential

All SDK authentication SHOULD use:
- `DefaultAzureCredential`

---

# Microsoft Graph Foundations

## Purpose

BusTerminal will eventually support:
- User lookup
- Group lookup
- RBAC automation
- Tenant metadata inspection
- Automation workflows

## Graph Authentication

Graph access MUST use:
- Managed identities for app-only flows
- Delegated flows only when necessary

---

# Infrastructure Authorization

## Infrastructure as Code

Infrastructure deployments MUST use:
- OpenTofu

OpenTofu modules SHOULD:
- Encapsulate RBAC assignments
- Encapsulate managed identity provisioning
- Encapsulate federated credential configuration
- Avoid inline credential definitions
- Support environment portability

## CI/CD Authentication

CI/CD pipelines MUST:
- Use federated identity authentication
- Avoid stored secrets
- Avoid publish profiles

Preferred:
- GitHub OIDC federation
- Azure DevOps workload federation

---

# Token Acquisition Patterns

## Backend to Azure Services

Preferred:
- `DefaultAzureCredential`

## Backend to Internal APIs

Preferred:
- Managed identity token acquisition

## Frontend to Backend APIs

Preferred:
- Access token via MSAL

---

# Security Requirements

## Logging Requirements

Authentication events SHOULD log:
- Login success/failure
- Authorization failures
- Token validation failures
- Privilege escalation attempts

Sensitive token contents MUST NOT be logged.

## TLS Requirements

All endpoints MUST require HTTPS.

---

# Required Deliverables

## Backend Deliverables

- JWT authentication middleware
- Authorization policy framework
- Claims normalization layer
- Role enforcement middleware
- Graph client foundation
- Managed identity abstraction utilities

## Frontend Deliverables

- MSAL integration
- Login/logout UX
- Protected route handling
- Session management
- Token acquisition abstraction
- Role-aware UI rendering

## Infrastructure Deliverables

- Entra app registrations
- OpenTofu modules for identity resources
- Managed identity provisioning
- Federated credential setup
- RBAC assignments
- Key Vault RBAC configuration

## Documentation Deliverables

- Identity architecture diagrams
- Auth sequence diagrams
- RBAC mapping documentation
- Managed identity inventory
- Graph permissions inventory

---

# Acceptance Criteria

This slice is complete when:

- Users can authenticate via Entra ID
- Frontend acquires tokens successfully
- Backend validates JWTs correctly
- Role-based authorization works end-to-end
- Managed identities authenticate to Azure services
- No static Azure credentials exist in application code
- Internal APIs require OAuth authentication
- Graph client foundation is implemented
- CI/CD uses workload federation
- OpenTofu identity modules are implemented
- Authentication and authorization flows are documented
