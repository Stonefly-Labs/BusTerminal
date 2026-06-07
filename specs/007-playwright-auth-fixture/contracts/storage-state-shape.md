# Contract: Storage State Shape

**File pattern**: `web/tests/.auth/<persona>.json`
**Producer**: `web/tests/auth/global-setup.ts` via `BrowserContext.storageState({ path })`
**Consumer**: `web/tests/fixtures/auth.ts` via `test.use({ storageState })`
**Playwright version constraint**: `@playwright/test >= 1.41` (already satisfied at `^1.60`)

---

## Why this exists as a contract

The storage state JSON is Playwright's internal serialization format. We document it explicitly because:

1. **MSAL stores session data in `sessionStorage`** (`web/lib/auth/msal-config.ts:21`). If a future Playwright version regresses sessionStorage capture, the fixture silently breaks — every persona's file would be empty of MSAL data and every authenticated test would land on the sign-in redirect. The contract here makes the dependency explicit.
2. **Test authors should never edit these files by hand.** Hand-crafted entries that work today drift on every `@azure/msal-browser` upgrade.

---

## Required structure (minimum)

```jsonc
{
  "cookies": [
    // Typically empty for this SPA. Present field; may be [].
  ],
  "origins": [
    {
      "origin": "http://localhost:3000",          // baseURL at globalSetup time
      "localStorage": [
        // Typically empty given MSAL cacheLocation is sessionStorage.
      ],
      "sessionStorage": [
        // MUST be non-empty for personas other than 'none' if the user is
        // authenticated. Records are MSAL-internal — names vary by msal-browser
        // version but at minimum include account, id-token, access-token, and
        // refresh-token entries. The fixture does not parse these; it only
        // requires their presence (asserted indirectly via the post-load
        // navigation succeeding without redirect to login).
        { "name": "msal.account.keys", "value": "[\"...\"]" },
        { "name": "msal.<scope-hash>", "value": "{...}" }
        // ...
      ]
    }
  ]
}
```

---

## Hard requirements

| Requirement | Enforced by | Failure mode if violated |
|---|---|---|
| Top-level `origins` array exists | Playwright | Load throws — globalSetup fails loudly. |
| At least one `origin` entry whose `origin` matches the test's `baseURL` | Playwright + AuthGuard behavior | Page loads but `AuthGuard` redirects to sign-in. Captured as "fixture mis-configured" via FR-014's diagnostic path. |
| For non-`none` personas: `sessionStorage` is non-empty for the matching origin | globalSetup post-write assertion (R2) | globalSetup fails loudly with persona-scoped diagnostic. |
| For `none` persona: `sessionStorage` is non-empty (authenticated, role-empty) | same | same |

`none` is **not** the same as an empty file — the user is authenticated, so MSAL session entries are present. What makes `none` "no role" is that the access token's `roles` claim is empty, not that the storage is empty.

---

## Soft conventions

- **Pretty-printed for diffability** when investigating issues, but Playwright's default serialization is compact. Either form is valid.
- **One file per persona**, named exactly `<persona>.json`. The fixture derives the path from the persona enum; renaming the file silently breaks the mapping.
- **Origin should match `process.env.PLAYWRIGHT_BASE_URL` or default `http://localhost:3000`.** A CI-captured file should never be reused locally and vice versa; globalSetup unconditionally regenerates per run if the file's origin disagrees with `baseURL`.

---

## What we do NOT depend on

- Specific MSAL key names. They change between `@azure/msal-browser` minor versions. The fixture's contract is "the file makes the AuthGuard admit the user without a sign-in redirect," not "the file contains key X with value Y."
- Cookie contents. The SPA does not rely on cookies for auth state.
- Origin port matching exactly. Playwright matches on full origin including port; we keep CI and local on the same `http://localhost:3000` to avoid origin mismatches.

---

## Failure detection at globalSetup

After `context.storageState({ path })` writes the file, globalSetup re-reads it and asserts:

1. The file parses as JSON.
2. `origins[]` contains an entry whose `origin === baseURL`.
3. For non-`none` personas, that entry's `sessionStorage` has length > 0.
4. The same context (still alive in globalSetup) successfully fetches `/whoami` and the response's role claims match `PersonaConfig.expectedRoleAssignments`.

Any failure aborts globalSetup with a persona-scoped error message and points the operator at `quickstart.md` (FR-014).
