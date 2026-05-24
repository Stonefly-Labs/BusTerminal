# Spec 003 — Polish punch list

These are the Phase 9 tasks that **cannot be fully closed from a code-editor session**. Each one needs either a human/manual verification, a live cloud auth context, or a tool/binary that isn't sensibly run from the spec-implementer agent's shell. They are tracked here so they don't get lost.

Run them in the order below before merging the slice, or queue them as separate follow-up tickets. **Until they're each ticked, slice 003 is "code-complete, not verified-complete."**

---

## 🚨 ⭐ Highest-priority manual step (this isn't a polish task — it's the deploy gate) ⭐ 🚨

**Grant Graph admin consent on `bt-dev-api` after CD applies the slice to dev.**

A tenant admin (`a-christopher@chrishou.se`) must run:

```bash
az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3
az ad app permission admin-consent --id 9fb329a3-7b5b-4fdf-a46a-71f7df1d6716
```

Why it can't be automated: `User.Read.All` (application) requires tenant-admin consent. The grant lives in the Entra directory audit log, not in `tofu apply` history (FR-024 / research § 9).

Until granted:
- SC-009 (Graph self-resolve first-call success) cannot pass.
- `GET /probe/developer` returns `graphResolvedDisplayName: null` and logs a warning at `EventId=3001`.

After running it: update `contracts/graph-permissions-inventory.md` § "Consent state by environment" with today's date and the granting admin's UPN.

---

## T089 — Fresh-machine quickstart walkthrough

**Goal**: Validate that `specs/003-auth-and-identity/quickstart.md` is correct end-to-end on a clean machine. Target: ≤ 30 minutes from `git clone` to a working role-aware local stack.

**Why deferred to user**: requires a machine that hasn't already cached prerequisites, run `az login`, or wired tenant config. The spec-implementer agent's machine is by definition pre-warmed.

**Protocol**:

1. On a fresh machine (or a clean container / VM):
   - Clone the repo.
   - Read `quickstart.md` start-to-finish. Time yourself.
   - Stop reading and start *doing*. Follow every command exactly.
2. When a step fails or is ambiguous, record the deviation inline (file path + line range) and fix it in-place.
3. Stop the timer when:
   - The local stack runs (`pwsh scripts/start-local.ps1`).
   - Sign-in via MSAL completes.
   - `/platform-status` renders with your `BusTerminal.Developer` role visible.
4. If the timer exceeds 30 minutes, that's a *runbook* defect — fix `quickstart.md`, not the implementation.

**Done when**: the timer captures a ≤ 30 min run with no in-place fixes needed.

---

## T095 — `axe` accessibility scan

**Goal**: Confirm WCAG 2.2 AA conformance for the no-access page and role-aware affordances added in this slice.

**Why deferred to user**: requires a running dev stack + a browser context to inject `axe`. The Vitest+RTL component tests (`web/components/auth/__tests__/role-aware-button.test.tsx` and friends) cover structural a11y (roles, labels, `aria-*`), but `axe` against a rendered page catches contrast / focus-order / landmark issues the component harness can't see.

**Protocol**:

1. Start the local stack (`pwsh scripts/start-local.ps1`).
2. Sign in (any role).
3. Run `axe` against:
   - `/no-access` (sign in with a no-role user; from your existing session, you can simulate by signing out and back in with a fresh-test-user that has no role grant).
   - `/platform-status` (sign in as `BusTerminal.Developer`).
   - The first authenticated page where role-aware buttons are visible (currently `/platform-status`'s role-aware nav header).
4. Resolve every WCAG 2.2 AA violation. AAA violations are nice-to-fix; AA is mandatory.

You can use either:
- The **axe DevTools** browser extension (Chrome/Firefox) — point-and-click, no install in repo.
- The **`@axe-core/playwright`** npm package wired into a one-off Playwright run (not committed) — script-friendly.

**Done when**: zero WCAG 2.2 AA violations on each of the three pages.

---

## T098 — `tofu plan` against `iac/environments/dev/`

**Goal**: Confirm the slice's IaC changes plan as **additive only** — new modules, new role definitions, the new Graph permission grant, and zero-effect refactors (`moved` blocks). No destructive changes.

**Why deferred to user (PR-CI)**: requires the dev azurerm backend (OIDC-federated, accessible only from the GitHub Actions runner with the dev pipeline managed identity) plus all `TF_VAR_*` values held as repo/env secrets. Local `tofu validate -backend=false` is clean — the structural composition is verified — but the *plan diff against current dev state* must come from the CI workflow.

**Protocol**:

1. Open the PR.
2. Wait for the `iac-validate` workflow's `plan-dev` job to complete.
3. Open the PR comment titled **"OpenTofu plan — `dev`"**.
4. Assert the plan contains **only**:
   - **Resources to add**: the `azuread_application_api_access` for `User.Read.All` (added by `iac/modules/graph-permissions/`), plus the new `module "graph_permissions"` wiring at `iac/environments/dev/main.tf`.
   - Any earlier-slice additions already reviewed in their own PRs (workload identities for the role probes, federated credentials added in slice 003's earlier user stories).
   - **`moved` blocks** are zero-effect refactors and are acceptable.
5. **Assert the plan contains zero resources to destroy.** If the diff shows any `-` lines, treat as a blocker and investigate.

**Done when**: the PR plan comment shows the expected adds only.

---

## Summary

| Task | Owner | Tool/Context required | Done signal |
|---|---|---|---|
| **Graph admin consent** (post-merge) | tenant admin (`a-christopher@chrishou.se`) | `az` CLI w/ tenant-admin role | `contracts/graph-permissions-inventory.md` updated |
| **T089** Fresh-machine quickstart | anyone (preferably someone not already onboarded) | clean machine | ≤ 30 min walk-through |
| **T095** Axe a11y scan | anyone | local stack + browser + axe | zero WCAG 2.2 AA violations |
| **T098** Tofu plan in CI | reviewer | the PR's `plan-dev` job | additive-only diff, zero destructions |

Once each row is checked, slice 003 is verified-complete and ready to merge.
