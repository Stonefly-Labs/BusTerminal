# Quickstart: Solution Foundation

**Feature**: 002-solution-foundation
**Audience**: A developer who has just cloned the repository and wants to (a) run the solution locally end-to-end and (b) understand how it gets deployed to Azure.
**Success criterion**: SC-001 — under 30 minutes from a fresh clone to a working local stack on a supported machine.

This is the slice-level quickstart. It will be promoted to `docs/local-development.md` and `docs/deploying-environments.md` during implementation.

---

## 1. Prerequisites

Install once (any modern macOS, Windows, or Linux dev machine):

| Tool | Minimum version | Used for |
|------|-----------------|----------|
| .NET SDK | 10.0 | Backend build/run/test |
| Node.js | 22 LTS | Frontend build/run/test |
| pnpm | latest | Frontend package manager (slice-001 convention) |
| Docker Desktop (or Podman) | latest | Containerized local stack + ACR pushes |
| OpenTofu | 1.10+ | Infrastructure as code |
| Azure CLI | 2.60+ | Azure auth + bootstrap |
| PowerShell | 7.4+ (Core) | Dev scripts (primary shell) |
| `gh` CLI | 2.40+ | Optional — for inspecting PR runs |

Verify with `scripts/bootstrap.ps1` (or `scripts/bootstrap.sh`) — both check tool versions and print any gaps.

---

## 2. First-time local setup

```powershell
# 1. Clone (already done if you're reading this from inside the repo)
git clone https://github.com/<org>/BusTerminal.git
cd BusTerminal

# 2. Install JS dependencies (uses pnpm from the slice-001 workspace)
pnpm install --frozen-lockfile

# 3. Restore .NET dependencies
dotnet restore api/BusTerminal.sln

# 4. (Optional) seed your local .env files from templates
Copy-Item web/.env.local.example web/.env.local
Copy-Item api/BusTerminal.Api/appsettings.Development.json.example `
          api/BusTerminal.Api/appsettings.Development.json
```

The `.env.local` and `appsettings.Development.json` templates default to **mock-auth mode**, so no Entra ID tenant is required to run locally. To exercise the real Entra flow locally, fill in the placeholders with your dev app-registration values (see `docs/identity-and-secrets.md`).

---

## 3. Run the stack locally (native, primary)

```powershell
pwsh scripts/start-local.ps1
```

This spawns:

- `dotnet watch` for the backend on `http://localhost:5000` (logs to terminal)
- `pnpm dev` for the frontend on `http://localhost:3000` (logs to terminal)

Open `http://localhost:3000` and you'll be redirected to the sign-in page. In mock-auth mode, a single "Continue as Dev User" button completes the loop. After sign-in you land on `/platform-status`, which calls `/whoami` on the backend and renders your identity + the request correlation ID.

**Expected end-to-end flow**:

1. Frontend renders the navigation shell (header + sidebar) consuming slice-001 design tokens.
2. Sign-in completes (mock or real Entra).
3. Platform-status page makes a `fetch` to `http://localhost:5000/whoami` with `Authorization: Bearer <token>` and a generated `traceparent` header.
4. Backend validates the token, records a span with the inbound trace ID, and responds with principal + correlation info.
5. Page renders the identity card and the correlation block.

If anything fails, the terminal output for either process will show the structured error.

---

## 4. Run the stack locally (containerized, optional)

```bash
docker compose up --build
```

This builds both `Dockerfile`s and runs the resulting images with the same port mapping as native mode. Useful for catching container-only bugs before pushing to Azure.

---

## 5. Run the tests

```powershell
# Frontend
pnpm --filter web test           # Vitest
pnpm --filter web test:e2e       # Playwright (assumes local stack is up)
pnpm --filter web test:a11y      # axe

# Backend
dotnet test api/BusTerminal.sln

# IaC
tofu -chdir=iac/environments/dev validate
tofu -chdir=iac/environments/dev plan -var-file=terraform.tfvars
```

Convenience wrapper: `scripts/test-all.ps1` runs the lot.

---

## 6. Bootstrap a fresh Azure environment (one-time)

> This step is only needed when **standing up a new Azure subscription/environment for the first time**, not for everyday development.

You have two paths (per FR-082b):

### Path A — OpenTofu bootstrap module (recommended)

```powershell
az login
az account set --subscription <subscription-id>

# Run the one-time platform-bootstrap module
pwsh scripts/bootstrap-platform.ps1 -EnvironmentName dev -GitHubOrgRepo <org>/BusTerminal
```

The script wraps `tofu apply` inside `iac/platform-bootstrap/`. It provisions:

- A dedicated state-storage resource group + storage account + `tfstate` container
- A user-assigned managed identity for the GitHub Actions `dev` environment
- A federated identity credential on that identity (subject `repo:<org>/BusTerminal:environment:dev`)
- The RBAC role assignments the pipeline needs

The module emits the values you need to set as GitHub repository variables:
- `AZURE_CLIENT_ID` (the bootstrap-created managed identity)
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

### Path B — Manual `az` CLI walkthrough

See `scripts/bootstrap-platform-manual.md` for the equivalent step-by-step procedure (~15 commands) for developers who prefer not to run the OpenTofu module against their own subscription.

After either path, the Entra ID app registration is created manually one-time (Entra-level permissions usually aren't pipeline-delegable). The walkthrough is in `docs/identity-and-secrets.md`.

---

## 7. Deploy to dev (automated)

Push to `main`:

```powershell
git push origin main
```

The `cd-dev.yml` workflow runs and:

1. Authenticates to Azure via OIDC federation (no secrets used)
2. Builds both container images
3. Pushes them to ACR with tags `<git-sha>` and `latest`
4. Runs `tofu apply` against `iac/environments/dev/` (idempotent — no changes when nothing changed)
5. Updates the frontend and backend Container App revisions to the new image tags
6. Waits for the new revisions to report healthy via `/healthz/ready`
7. Optionally runs a post-deploy smoke check that calls `/whoami` against the deployed environment

**Target time** (SC-002): under 20 minutes for a no-infrastructure-change deploy.

---

## 8. Adding a new environment (e.g., `test`)

This is what SC-006 measures (under 1 working day, documentation-only). The procedure:

1. Copy `iac/environments/dev/` to `iac/environments/test/` and adjust `terraform.tfvars` for environment-specific values.
2. Run the bootstrap module (Path A above) with `-EnvironmentName test` to provision the test pipeline identity and federated credential.
3. Create a GitHub environment named `test` in repo settings, adding the bootstrap-output values as environment variables.
4. Add a `cd-test.yml` workflow modeled on `cd-dev.yml` but targeting the `test` environment.
5. Commit and merge. The first run provisions the `test` environment.

No source code in any module changes. That's the test of FR-081, FR-083, and SC-006.

---

## 9. Observing a deployed environment

In the Azure Portal:
- **Application Insights → Live Metrics**: real-time request volume and failure rate
- **Application Insights → Transaction Search**: query by correlation ID (paste the `traceId` shown on the platform-status page to see the request from frontend through backend)
- **Application Insights → Application Map**: dependency view (frontend → backend, backend → Entra ID metadata, backend → Key Vault)
- **Log Analytics Workspace → Logs**: KQL access to everything (workload logs in `ContainerAppConsoleLogs_CL`, system logs in `ContainerAppSystemLogs_CL`, plus all Azure resource diagnostic logs)

Useful starter queries are documented in `docs/observability.md`.

---

## 10. Troubleshooting

| Symptom | Most likely cause | Where to look |
|---------|-------------------|---------------|
| Local frontend returns 401 on every backend call | Mock-auth env var off but real Entra creds not filled in | `web/.env.local` |
| Pipeline OIDC step fails | Federated credential `subject` doesn't match the deploy environment name | Azure Portal → Managed Identity → Federated credentials |
| Container Apps revision stuck in "Activating" | Startup probe failing (Key Vault unreachable or Entra metadata fetch slow) | App Insights → Failures + Container App → Revision logs |
| `tofu plan` shows unexpected changes on a re-run | Drift: someone changed a resource outside IaC | Compare plan output against last applied state; reconcile or revert manually |
| `gitleaks` fails the CI on a known-safe placeholder | Pattern not in `.gitleaks.toml` allowlist | Update the allowlist with the specific pattern, not by raising the threshold |

---

## 11. Where to go next

- For deep architecture context: `docs/architecture.md`
- For identity + secrets: `docs/identity-and-secrets.md`
- For observability dashboards and queries: `docs/observability.md`
- For the slice's specification: `specs/002-solution-foundation/spec.md`
- For the plan: `specs/002-solution-foundation/plan.md`

When this slice graduates, this quickstart will be promoted into `docs/` and the spec-level copy archived.
