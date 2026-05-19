# Local Development

This doc is the canonical onboarding guide for BusTerminal. It covers cloning the
repo, installing prerequisites, running the stack natively or in containers, and
the troubleshooting paths a new engineer is most likely to hit.

The target time-to-first-screen for a new developer (SC-001) is **under 30
minutes** from a fresh clone on a supported machine.

For deeper context, see `specs/002-solution-foundation/quickstart.md`.

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
| Azure CLI | 2.60+ | Azure sign-in + bootstrap |
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
```

The seeded `appsettings.Development.json` enables **mock authentication mode**
(`AzureAd:TenantId = "development"`), so no real Entra ID tenant is required to
run locally. To exercise the real Entra flow, replace the placeholders with your
dev app-registration values (see `docs/identity-and-secrets.md`).

---

## 3. Run the stack — native (recommended for inner-loop development)

```bash
./scripts/start-local.sh
# or
pwsh ./scripts/start-local.ps1
```

The script spawns:

| Component | URL | Process |
|-----------|-----|---------|
| Backend | `http://localhost:5000` | `dotnet watch` |
| Frontend | `http://localhost:3000` | `pnpm dev` (Next.js with Turbopack) |

Ctrl-C stops both cleanly. Logs are interleaved, prefixed with `[api]` and
`[web]`.

Open `http://localhost:3000` — you should be redirected to `/signin`, see the
"Continue as Dev User" button, click it, and land on `/platform-status` showing
the dev user's identity and a non-empty correlation ID.

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

---

## 6. Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `/signin` shows but the dev-mode button doesn't appear | `AZURE_AD_TENANT_ID` is not `development`. Re-seed `appsettings.Development.json` or export the env var before running `start-local`. |
| Sign-in redirects back to `/signin` instead of `/platform-status` | `NEXTAUTH_SECRET` / `AUTH_SECRET` not set or changed since the last sign-in. Clear cookies and re-run. The local scripts seed a dev-only secret automatically. |
| Backend 401 even with mock auth | The dev tenant value is missing on the API process. Confirm `AzureAd__TenantId=development` is reaching the API (env vars or appsettings). |
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
| _Pending_ | _Pending_ | _Pending_ | _Pending_ | First measurement is captured by T060. |
