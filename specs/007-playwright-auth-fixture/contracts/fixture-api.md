# Contract: Fixture TypeScript API

**File**: `web/tests/fixtures/auth.ts` (to be created)
**Consumer**: Every E2E spec under `web/tests/e2e/**/*.spec.ts`
**Provider**: `@playwright/test` `test.extend(...)` factory

This is the **public** surface the fixture exposes to test authors. The internal implementation (globalSetup, sign-in helper, persona config) is private and may evolve.

---

## Exported types

```typescript
export type Persona = 'reader' | 'operator' | 'admin' | 'none';

export interface AuthOptions {
  /**
   * The persona whose authenticated browser session should seed this test's
   * context. Omit to run with no seeded session (used by specs that exercise
   * the pre-auth UX).
   *
   * Setting `persona` causes the worker fixture to load
   * `web/tests/.auth/<persona>.json` as the context's `storageState`. The
   * file is produced once per CI run by Playwright's globalSetup; locally,
   * it is reused across runs until the underlying refresh material expires
   * or `scripts/e2e-test-identities/rotate-password.sh` invalidates it.
   */
  persona: Persona | undefined;
}
```

---

## Exported `test` factory

```typescript
import { test as base, expect } from '@playwright/test';

export const test = base.extend<AuthOptions>({
  persona: [undefined, { option: true }],
  storageState: async ({ persona }, use) => {
    if (persona === undefined) {
      await use(undefined); // no seeded session
      return;
    }
    await use(`web/tests/.auth/${persona}.json`);
  },
});

export { expect };
```

---

## Consumer usage

**Spec-file scope (canonical pattern)** — every test in the file uses the same persona:

```typescript
import { test, expect } from '@/tests/fixtures/auth';

test.use({ persona: 'reader' });

test.describe('role-aware affordances', () => {
  test('reader sees no Operator nav entries', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('nav-operate')).toHaveCount(0);
  });
});
```

**Mixed personas in one file** — use `test.describe(...)` with nested `test.use`:

```typescript
import { test, expect } from '@/tests/fixtures/auth';

test.describe('admin can delete', () => {
  test.use({ persona: 'admin' });
  test('delete button works', async ({ page }) => { /* ... */ });
});

test.describe('reader cannot delete', () => {
  test.use({ persona: 'reader' });
  test('delete button is disabled', async ({ page }) => { /* ... */ });
});
```

**Unauthenticated spec** — omit `persona` entirely:

```typescript
import { test, expect } from '@/tests/fixtures/auth';

test('malformed bearer returns 401', async ({ request }) => {
  // No persona set — request has no seeded auth state.
  const res = await request.get('/whoami', {
    headers: { authorization: 'Bearer malformed.token.value' },
  });
  expect(res.status()).toBe(401);
});
```

---

## Guarantees

1. **Session isolation**: each test's browser context is created from its own clone of the storageState file. No two tests share a mutable session.
2. **No interactive prompts**: provided the storageState file is valid (FR-005), the test never sees a sign-in redirect, MFA prompt, or consent dialog.
3. **No credential leakage**: the fixture only reads the storageState file. It never reads from Key Vault. It never reads or writes the persona's password. Passwords are touched only by globalSetup (a separate Playwright project) and `rotate-password.sh`.
4. **Trace Context propagation preserved**: the fixture does not intercept or modify the page's HTTP client; W3C Trace Context propagation (`traceparent` / `tracestate`) behaves identically to an interactively-signed-in user.

---

## Non-guarantees

1. **Not direct-API auth**. Per FR-017, the fixture does not expose persona bearer tokens for direct test-runner-to-API calls. A future helper may do so under a separate, explicitly-named export.
2. **Not sign-out**. The fixture is acquire-only. Tests asserting sign-out behavior use the in-page MSAL sign-out UI directly.
3. **Not cross-tenant**. The fixture is hard-wired to the dev tenant. Multi-tenant testing is out of scope.

---

## Migration of currently-fixme'd specs

Each suspended spec must:

1. Replace its import `from '@playwright/test'` with `from '@/tests/fixtures/auth'` (or the project-relative path).
2. Add `test.use({ persona: '<persona>' });` at the top.
3. Remove the `test.fixme(...)` wrappers, leaving the inner test body unchanged.

Persona assignments per file (canonical):

| Spec file | Persona |
|---|---|
| `msal-sign-in-and-whoami.spec.ts` (sign-in-cycle case) | `reader` |
| `platform-status.spec.ts` | `reader` |
| `no-access-experience.spec.ts` | `none` |
| `role-aware-affordances.spec.ts` | `reader` |
| `registry/create-browse.e2e.spec.ts` | `operator` |
| `registry/delete-blocked.e2e.spec.ts` | `operator` |
| `registry/edit-conflict.e2e.spec.ts` | `operator` |
| `registry/relationships-audit.e2e.spec.ts` | `reader` |
| `registry/sc-010-time-to-find.e2e.spec.ts` | `reader` |
| `registry/search.e2e.spec.ts` | `reader` |
| `registry/unauthorized-state.e2e.spec.ts` | `reader` (the 401 path is asserted via API; the page is in an authenticated-reader context) |
