# Local Development

This doc is the canonical onboarding guide for BusTerminal. It covers cloning the
repo, installing prerequisites, running the stack natively or in containers, and
the troubleshooting paths a new engineer is most likely to hit.

The target time-to-first-screen for a new developer (SC-001) is **under 30
minutes** from a fresh clone on a supported machine.

For the slice-level identity & auth context, see
`specs/003-auth-and-identity/quickstart.md` — this document is the durable
home for the developer-onboarding subset of that quickstart.

---

## 0. Before you do anything else

**You must have a working identity in the BusTerminal dev tenant.** MSAL no
longer ships a frontend mock provider — local sign-in goes to the real dev
tenant, and local backend code that reaches Azure (Key Vault, future Cosmos /
Search / Service Bus / etc.) uses your `az`-resolved identity via
`IAzureCredentialFactory` (FR-018).

Before any local-Azure work:

1. You must have an **Entra account in the BusTerminal dev tenant**
   (`596c1564-6e95-4c35-a80b-2dbe45a162f3`).
2. An Admin must have granted your account at least the **`BusTerminal.Developer`**
   app role on the `bt-dev-api` app registration. (Operator/Admin if you need
   write access locally.) See `docs/identity-role-administration.md` § Part B
   for the grant procedure.
3. You must be signed into Azure CLI with that account:

   ```bash
   az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3
   az account set --subscription 08b37dc0-0011-4841-84c0-0349a5c65883
   ```

`scripts/start-local.{sh,ps1}` validates this on launch — when the backend is
configured to reach a real Key Vault (`AZURE_KEY_VAULT_URI` set), the script
aborts with a remediation pointer if you're signed out or in the wrong tenant.
For pure mock-tenant local dev (no Azure dependencies), the check is advisory.

This document's procedures assume those three steps are done. The slice-level
quickstart (`specs/003-auth-and-identity/quickstart.md` § C.1) is the operator
view of the same prerequisite.

---

## 1. Prerequisites

Install these once. Versions are minimums.

| Tool | Minimum version | Used for |
|------|-----------------|----------|
| .NET SDK | 10.0 | Backend build / run / test |
| Node.js | 22 LTS | Frontend build / run / test |
| pnpm | 11+ | Frontend package manager |
| Docker Desktop / Podman | latest | Containerized local stack |
| OpenTofu | 1.10+ | Infrastructure as code |
| Azure CLI | 2.60+ | Azure sign-in (real-Azure dev paths) |
| PowerShell | 7.4+ (Core) | Cross-platform dev scripts |
| `gh` CLI | 2.40+ (optional) | Inspecting PR runs from the CLI |

Verify your installation with one of:

```bash
./scripts/bootstrap.sh
# or
pwsh ./scripts/bootstrap.ps1
```

The bootstrap script prints which tools satisfy the minimum and which need
attention. It does NOT install anything.

---

## 2. First-time clone setup

```bash
git clone https://github.com/<org>/BusTerminal.git
cd BusTerminal

# Frontend dependencies
pnpm --filter web install

# Backend dependencies
dotnet restore api/BusTerminal.slnx

# Seed the local dev appsettings from the template (idempotent — start-local
# also does this on first run)
cp api/BusTerminal.Api/appsettings.Development.json.example \
   api/BusTerminal.Api/appsettings.Development.json

# Seed the frontend MSAL env from its template
cp web/.env.local.example web/.env.local
```

Edit `web/.env.local` and `api/BusTerminal.Api/appsettings.Development.json`
with the dev-tenant values from
`specs/003-auth-and-identity/quickstart.md` § C.3 (the tenant id, the web app
registration client id, the API scope, and the API audience). Both files are
gitignored. **No client secrets are required** — the SPA uses Authorization
Code + PKCE; the backend validates Entra-issued tokens with no client
credentials of its own; Azure-service access uses `DefaultAzureCredential`
resolving to your `az login` identity.

If you only want to exercise the backend without round-tripping Entra (for
integration-test-style probing), the backend supports a `tenant=development`
mock-auth mode — set `AzureAd:TenantId` to `development` in
`appsettings.Development.json` and use the `X-Mock-Roles` header on requests.
The frontend has no equivalent — MSAL always signs into the real dev tenant.

---

## 3. Run the stack — native (recommended for inner-loop development)

```bash
./scripts/start-local.sh
# or
pwsh ./scripts/start-local.ps1
```

The script first verifies your Azure CLI sign-in (see § 0), then spawns:

| Component | URL | Process |
|-----------|-----|---------|
| Backend | `http://localhost:5000` | `dotnet watch` |
| Frontend | `http://localhost:3000` | `pnpm dev` (Next.js with Turbopack) |

Ctrl-C stops both cleanly. Logs are interleaved, prefixed with `[api]` and
`[web]`.

Open `http://localhost:3000` — MSAL redirects you to Entra, you sign in with
your dev-tenant account, and you land on `/platform-status`. The page calls
`/whoami` and shows your identity (display name, oid, tenant), your effective
`BusTerminal.*` roles, and the W3C correlation id. If you have no role
assigned, you land on `/no-access` instead — that's expected; loop back to
step 0.2 to get a role granted.

---

## 4. Run the stack — containerized

```bash
./scripts/start-local-containers.sh
# or
pwsh ./scripts/start-local-containers.ps1
```

This is a thin wrapper around `docker compose up --build`. The compose file at
the repo root builds both Dockerfiles and runs them with the same port mapping
as the native stack. Use this when you need parity with the Container Apps
runtime (cold-start behavior, chiseled image surface, etc.).

To reach Azure dependencies from inside containers, mount your
`~/.azure` directory into the API container so `AzureCliCredential` inside the
DefaultAzureCredential chain can read your tokens. (The compose file's `api`
service already does this on platforms that support volume mounts.)

---

## 5. Run the tests

```bash
./scripts/test-all.sh
# or
pwsh ./scripts/test-all.ps1
```

This runs frontend Vitest, frontend typecheck, backend xUnit, and the IaC
`tofu validate` (when the dev environment composition exists). Accessibility
tests are opt-in (`RUN_A11Y=1 ./scripts/test-all.sh`) because they spin up a
real browser.

Individual targeting:

```bash
pnpm --filter web test            # Vitest unit + component
pnpm --filter web typecheck       # tsc --noEmit
pnpm --filter web test:e2e        # Playwright e2e
pnpm --filter web test:a11y       # Playwright accessibility
dotnet test api/BusTerminal.slnx  # Backend
```

### Canonical-store fixture and ops CLI (`tools/load-fixtures`)

Spec 004 introduces a `dotnet`-based CLI at `tools/load-fixtures/` for loading
fixture data into the canonical Cosmos store, exporting/importing portable
envelopes, inspecting individual resources, traversing the relationship graph,
and driving lifecycle operations. The project is included in
`api/BusTerminal.slnx` and references `BusTerminal.Api` directly so it shares
the domain model and persistence adapter.

```bash
dotnet run --project tools/load-fixtures -- --help
```

Subcommands are scaffolded in Phase 1 of spec 004 and filled in by later
tasks. The full operator runbook is at `specs/004-core-domain-model/quickstart.md`.

For the local emulator path, `docker compose up -d cosmos-emulator` brings up
the Cosmos DB Linux emulator on `https://localhost:8081` before running any
canonical-store integration tests or CLI commands.

---

## 6. Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `/signin` redirects to `login.microsoftonline.com` then errors `AADSTS50011 — redirect URI mismatch` | The SPA app registration is missing a redirect URI for your local URL. Add `http://localhost:3000` to the SPA platform's redirect URIs in the Entra portal. |
| `/signin` redirects to `login.microsoftonline.com` then errors `AADSTS700016 — application not found` | `NEXT_PUBLIC_AZURE_AD_CLIENT_ID` in `web/.env.local` is wrong or missing. Copy the value from `web/.env.local.example` and the dev tenant's app registration. |
| Sign-in completes but the app redirects to `/no-access` | Your dev-tenant account has not been assigned a `BusTerminal.*` app role. See `docs/identity-role-administration.md` for the grant procedure. |
| Backend startup fails with `Azure credentials unavailable. Run: az login --tenant 596c1564-...` | The backend's `IAzureCredentialFactory` couldn't resolve any credential while building configuration from Key Vault. Run the suggested `az login` command and restart. |
| Backend 401 `Bearer error="invalid_token"` against `/whoami` | The API audience doesn't match `NEXT_PUBLIC_API_SCOPE`. Compare `AzureAd:Audience` in `appsettings.Development.json` to the `NEXT_PUBLIC_API_SCOPE` URI in `web/.env.local`. |
| Backend 401 even with mock auth | The dev tenant value is missing on the API process. Confirm `AzureAd__TenantId=development` is reaching the API (env vars or appsettings). |
| `start-local` aborts with `AZURE_KEY_VAULT_URI is set; aborting` | You set `AZURE_KEY_VAULT_URI` for a real-Azure run but aren't signed in. Run `az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3`. |
| Web cannot reach the backend | Check `NEXT_PUBLIC_API_BASE_URL` — defaults to `http://localhost:5000`. The container build defaults to `http://api:8080` inside the compose network. |
| `dotnet watch` keeps restarting | Confirm no other process is bound to port 5000 — `lsof -i :5000` on macOS/Linux. |
| Playwright says "Executable doesn't exist" | Run `pnpm --filter web exec playwright install --with-deps`. |

---

## 7. Onboarding benchmark (SC-001)

The success criterion for this slice is **under 30 minutes** from a fresh
clone to a working local stack. The benchmark is captured below and should be
re-measured whenever the bootstrap surface materially changes.

| Run date | Operator | Machine | Result | Notes |
|----------|----------|---------|--------|-------|
| _Pending_ | _Pending_ | _Pending_ | _Pending_ | First measurement is captured by Phase 9 polish (T089). |
