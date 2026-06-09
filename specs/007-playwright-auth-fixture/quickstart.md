# Quickstart: Running E2E Auth Tests Locally

**Feature**: 007-playwright-auth-fixture (post-2026-06-08 pivot to mock auth — see research §R11)
**Audience**: Contributors who need to run the authenticated E2E suite on their workstation.

The suite now runs **without any Azure access**: no `az login`, no Key Vault, no Entra tenant. Auth is mocked client-side; the backend's existing `MockAuthenticationHandler` reads an `X-Mock-Roles` header and synthesises the request principal. Cosmos persistence runs against the local emulator from `docker-compose.yml`.

---

## Part A — Running locally (first time, clean checkout)

### Prerequisites

| Tool | Version | Already required? |
|---|---|---|
| Node.js | per `.nvmrc` | yes |
| pnpm | per `package.json` packageManager | yes |
| .NET SDK | 10.0.x | yes |
| Docker (or compatible) | recent | yes (for the Cosmos emulator) |

No Azure CLI required. No tenant access required. No credentials anywhere.

### Steps

1. **Install dependencies.**

   ```bash
   pnpm -C web install --frozen-lockfile
   pnpm -C web exec playwright install --with-deps
   ```

2. **Bring up the Cosmos emulator** (one-time per machine; container persists).

   ```bash
   docker compose up -d cosmos-emulator
   ```

   Wait for healthy state:

   ```bash
   docker compose ps cosmos-emulator   # expect: STATUS … (healthy)
   ```

3. **Start the backend** in mock-auth mode pointed at the emulator. The emulator's readiness probe binds port 8080, so override the API port with `BUSTERMINAL_API_PORT`:

   ```bash
   BUSTERMINAL_API_PORT=8090 \
   ASPNETCORE_ENVIRONMENT=Development \
   AzureAd__TenantId=development \
   AzureAd__ClientId=00000000-0000-0000-0000-000000000001 \
   AzureAd__Audience=api://00000000-0000-0000-0000-000000000001 \
   Cosmos__Endpoint=https://localhost:8081 \
   dotnet run --project api/BusTerminal.Api
   ```

   Wait for `Now listening on: http://[::]:8090`. Probe with `curl http://localhost:8090/healthz/ready` → `200`.

4. **Create `web/.env.local`** with the mock-mode env vars (gitignored):

   ```bash
   cat > web/.env.local <<'EOF'
   NEXT_PUBLIC_AUTH_MODE=mock
   NEXT_PUBLIC_API_BASE_URL=http://localhost:8090
   EOF
   ```

5. **Run the E2E suite.** Playwright's `webServer` block auto-starts the Next.js dev server.

   ```bash
   PLAYWRIGHT_API_BASE_URL=http://localhost:8090 \
   pnpm -C web test:e2e
   ```

   Or run a single previously-fixme'd spec to validate:

   ```bash
   PLAYWRIGHT_API_BASE_URL=http://localhost:8090 \
   pnpm -C web exec playwright test tests/e2e/role-aware-affordances.spec.ts
   ```

### Authoring a persona-annotated spec

```ts
import { test, expect } from "@/tests/fixtures/auth";

test.use({ persona: "operator" });  // 'reader' | 'operator' | 'admin' | 'none'

test("operator can create a namespace", async ({ page }) => {
  await page.goto("/registry/new/Namespace");
  // …
});
```

The fixture writes the persona into `sessionStorage["bt.e2e.persona"]` via `addInitScript` before any page script runs. The mock PCA reads it and synthesises a signed-in `AccountInfo`; the api-client adds `X-Mock-Roles: BusTerminal.Operator` to every outbound request; the backend mock handler reads the header and emits matching role claims.

Omit `test.use({ persona })` to run unauthenticated (used by specs that exercise the pre-auth UX or assert 401 responses on malformed bearer tokens).

### Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Failed to bind to address http://[::]:8080` when starting the backend | Cosmos emulator's readiness probe already owns 8080 | Set `BUSTERMINAL_API_PORT=8090` (or any other free port) and update the frontend's `NEXT_PUBLIC_API_BASE_URL` to match |
| Playwright reports the page stuck on "Redirecting…" | `NEXT_PUBLIC_AUTH_MODE` is not set on the Next.js process | Confirm `web/.env.local` has `NEXT_PUBLIC_AUTH_MODE=mock`; restart the dev server (kill any running `next dev` and let Playwright re-spawn) |
| Backend returns 401 / "WWW-Authenticate: Bearer" | `AzureAd__TenantId` is not the `"development"` sentinel; real Microsoft.Identity.Web validation is engaged | Re-export `AzureAd__TenantId=development` and restart the backend |
| Browser console: CORS error from `http://localhost:3000` → `http://localhost:8090` | Backend was started without `ASPNETCORE_ENVIRONMENT=Development`, so the dev-only CORS middleware is off | Restart the backend with `ASPNETCORE_ENVIRONMENT=Development` |
| Registry POST returns 404 from `http://localhost:3000/api/registry` | Next.js rewrites didn't pick up `BUSTERMINAL_DEV_API_TARGET` / `NEXT_PUBLIC_API_BASE_URL`; restart needed | Stop `next dev`, ensure the env var is set, restart Playwright (or `pnpm -C web dev`) |

---

## Part B — What is no longer required

The original 2026-06-07 quickstart documented a much longer setup (az login, Key Vault password pulls, real-Entra config, `tofu apply` of the test-identities module). All of that was removed when the 2026-06-08 pivot landed. For history see `research.md` §R11 and the commit that reverted `iac/modules/e2e-test-identities/`.

---

## Part C — Adding a new persona (e.g. `developer`)

Lower-cost than the original real-Entra path:

1. Add `'developer'` to the `Persona` union in `web/tests/auth/personas.ts`.
2. Add a `PERSONA_CONFIGS.developer` entry with a stable hardcoded GUID OID, `e2e-developer@mock.busterminal.dev` UPN, `displayName: "E2E Developer"`, and `expectedRoleAssignments: ["BusTerminal.Developer"]`.
3. Add `'developer'` to the `enum` in `contracts/persona-config.schema.json` (in `persona`, `upn`, `displayName`, `storageStatePath`, and `expectedRoleAssignments`).
4. The vitest schema test (`personas.config.test.ts`) picks up the new persona automatically via `PERSONA_NAMES`.

No tenant work, no IaC, no Key Vault.
