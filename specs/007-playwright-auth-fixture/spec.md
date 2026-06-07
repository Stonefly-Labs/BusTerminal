# Feature Specification: Playwright MSAL Auth Fixture for E2E Tests

**Feature Branch**: `007-playwright-auth-fixture`

**Created**: 2026-06-07

**Status**: Draft

**Input**: User description: "Build a Playwright auth fixture so E2E tests can exercise MSAL/Entra-protected routes in CI without a human in the loop."

## Clarifications

### Session 2026-06-07

- Q: Which credential-acquisition flow should the fixture use to seed the browser session against the dev Entra tenant? → A: Browser-automation sign-in performed once per persona during Playwright global setup, with the resulting browser `storageState` persisted and reused by every test that requests that persona.
- Q: Who provisions the four test identities and their role grants in the dev Entra tenant? → A: Managed by the project's OpenTofu modules — the four test users (or a brokering test app registration), their group/role memberships, and their application role assignments are declared as IaC and reproduced on every environment build. Test-user passwords remain in Key Vault, not in Tofu state.
- Q: What should the fixture authenticate — only the browser session, or also direct API calls made from the test runner outside the browser? → A: Browser session only. Backend calls made by the page during the test pick up tokens via the application's normal in-app flow. Runner-side direct-to-API calls are out of scope for this fixture; tests that need them can use a separate helper.
- Q: How should the fixture handle token expiry across a long suite run? → A: Let the in-page MSAL instance refresh silently using the refresh material in the captured `storageState` — the same behavior a real user's session would exhibit. The fixture does not intervene mid-run. It only re-runs the global-setup sign-in for a persona if that persona's `storageState` itself becomes unusable between runs (e.g., refresh-token expired or revoked).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Unblock authenticated route coverage with a single persona (Priority: P1)

A platform engineer running the E2E suite (locally or in CI) needs the suite to
exercise the bulk of authenticated-route behavior without manually clicking
through a Microsoft Entra sign-in. Today, every test that touches a page under
the authenticated route group is suspended (`test.fixme`) because no mechanism
seeds an authenticated session. The engineer wants to run the registry
end-to-end specs and the platform-status spec on every push and see real
pass/fail signal for the screens behind the auth wall.

**Why this priority**: This single capability — the ability to deliver an
authenticated browser session for one well-known persona into a Playwright
test — is the foundation that unblocks the majority of suspended tests. Eight
of the twelve currently-fixme'd test cases (the seven registry specs plus
platform-status) only need a generic authenticated user to be useful again.
Without this, no other priority is reachable.

**Independent Test**: Pick one currently-fixme'd registry spec (e.g.,
`web/tests/e2e/registry/create-browse.e2e.spec.ts`), have it request the
fixture, and run it locally against the dev environment. The test must reach
the post-auth UI, perform its assertions, and pass — proving the fixture
delivers a usable authenticated session without human input.

**Acceptance Scenarios**:

1. **Given** a Playwright spec annotated to request an authenticated session,
   **When** the test launches its browser context, **Then** the test arrives at
   the first navigation past the auth wall with a populated session and never
   sees the sign-in redirect.
2. **Given** a previously-suspended spec under
   `web/tests/e2e/registry/*.e2e.spec.ts` or
   `web/tests/e2e/platform-status.spec.ts`, **When** the suspension is removed
   and the spec adopts the fixture, **Then** the spec runs to completion
   against the dev environment and reports pass/fail based on its own
   assertions — not on auth scaffolding.
3. **Given** a developer running the suite locally with the documented setup
   complete, **When** they invoke the standard E2E command, **Then** no
   interactive browser prompts (sign-in, MFA, consent) appear and the suite
   advances unattended.

---

### User Story 2 - Cover role-aware behavior with multiple personas (Priority: P2)

A platform engineer needs the suite to exercise role-conditional UI behavior:
the role-aware affordances spec must run as a Reader-only user and confirm
Operator/Admin entries are hidden; the no-access spec must run as a
zero-role user and confirm the no-access page renders within the 2-second
budget. The engineer wants to declare a persona per test without copying auth
plumbing into each spec.

**Why this priority**: Multi-persona coverage is the second-largest source of
suspended coverage (the role-aware-affordances spec, the no-access-experience
spec, and at least one registry case that asserts denial). A single-persona
fixture covers the green path; this priority covers the boundary. It is
genuinely independent of P1 — a test can opt into any persona once the
mechanism exists — but it requires the P1 mechanism as a base, so it is
sequenced after.

**Independent Test**: With four personas available (Reader, Operator, Admin,
zero-role), select the role-aware-affordances spec, have it request the
Reader persona, and run it against the dev environment. Then separately run
the no-access-experience spec against the zero-role persona. Both must reach
their target screens and pass their assertions.

**Acceptance Scenarios**:

1. **Given** a test that declares the Reader persona, **When** the test loads
   the navigation shell, **Then** the rendered shell omits Operator and Admin
   entries and disables Mutate-Domain controls.
2. **Given** a test that declares the zero-role persona, **When** the test
   completes the post-sign-in navigation, **Then** the no-access page is
   visible within 2 seconds of the post-redirect timestamp.
3. **Given** a test that declares the Admin persona, **When** the test
   navigates to a destructive registry control, **Then** the control is
   enabled and the resulting action succeeds.
4. **Given** two specs in the same run that declare different personas,
   **When** the run completes, **Then** neither spec's session state leaks
   into the other and both pass.

---

### User Story 3 - Authenticated E2E coverage runs on every pull request (Priority: P3)

A platform engineer opening a pull request needs the authenticated-route E2E
suite to run automatically in CI and gate merge. Today the suite cannot run
under CI because no headless, non-interactive credential path exists. The
engineer also needs a documented, low-risk way to rotate test-user credentials
without halting CI for an extended window.

**Why this priority**: The fixture is only worth the investment if it runs on
every PR — otherwise regressions land. This priority covers the CI integration
and the credential-lifecycle documentation that turn a local capability into a
team-wide guarantee. It depends on P1 (and ideally P2) being in place.

**Independent Test**: Open a no-op pull request after this priority lands.
Verify the authenticated E2E job runs to completion against the dev
environment, the personas above resolve their credentials from the configured
secret store, and the run completes without exposing any credential in
workflow output or telemetry. Separately, walk through the documented rotation
procedure and confirm the suite returns to green after rotation.

**Acceptance Scenarios**:

1. **Given** a pull request opened against the default branch, **When** CI
   runs, **Then** the authenticated E2E job executes the previously-suspended
   specs and reports a pass/fail outcome on the PR.
2. **Given** the CI run, **When** logs and telemetry are inspected, **Then**
   no test-user credential, no access token, and no other secret value appears
   in any captured output.
3. **Given** the documented rotation procedure, **When** an operator follows
   it end-to-end, **Then** the next CI run after rotation passes without
   manual intervention beyond the documented steps.
4. **Given** the documented local-dev procedure, **When** a new contributor
   follows it from a clean checkout, **Then** they can run the authenticated
   E2E suite on their workstation without an interactive sign-in.

---

### Edge Cases

- **Long test runs that outlive a token's validity window.** The fixture must
  keep authenticated state usable for the duration of a normal suite run, even
  if that exceeds the initial token's lifetime, without injecting visible
  sign-in prompts.
- **Persona-scoped session leakage.** Two specs declaring different personas
  inside the same suite run must not bleed session state into each other —
  Admin actions must never be performed under a Reader session because of
  fixture reuse.
- **Concurrent worker contention on a single test identity.** Multiple
  Playwright workers may simultaneously request the same persona; the fixture
  must serve them without one worker's authentication invalidating another's.
- **W3C Trace Context assertion lights up.** The existing `traceparent`
  regex assertion authored in the msal sign-in spec (suspended today) must
  pass under the fixture, confirming UI-originated HTTP requests propagate
  trace context even though the session was non-interactively seeded.
- **Authenticated-route entrypoint missing for a persona that should have
  access.** If a persona that should reach a screen instead lands on the
  no-access page or the sign-in redirect, the test must fail with a clear
  signal pointing at the persona/role grant — not a generic timeout.
- **Credential rotation collides with an in-flight CI run.** The rotation
  procedure must not silently break a run already executing; the failure mode
  must be deterministic and the recovery path must be documented.
- **Local-dev environment has no access to the credential source.** A new
  contributor without the configured credential access must see a documented,
  actionable error — not a cryptic auth failure deep in the test run.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The test framework MUST provide a mechanism that, when requested
  by an E2E test, delivers a browser session already authenticated against the
  existing dev Entra tenant — no interactive sign-in, no MFA prompt, no
  consent dialog visible to the test or the developer.
- **FR-002**: The mechanism MUST support at least four distinct personas
  covering: a Reader-only identity, an Operator identity, an Admin identity,
  and a zero-role identity that is authenticated but holds none of the
  application's roles.
- **FR-003**: A test MUST be able to declare its desired persona via a single
  annotation or option — without copying credential-handling code into the
  spec.
- **FR-004**: All twelve currently-suspended test cases listed in the
  Assumptions section MUST be un-suspended and adopt the mechanism. After
  adoption, each MUST execute against the dev environment and report a
  meaningful pass/fail outcome based on its own assertions.
- **FR-005**: The mechanism MUST keep authenticated sessions usable for the
  full duration of a standard suite run without surfacing interactive auth
  prompts mid-run, even when the run exceeds the initial access token's
  validity window. Token refresh during a run MUST happen via the in-page
  client's normal silent-refresh path (i.e., the same path a real user's
  session would use) — the fixture does not intervene mid-run. The fixture
  re-acquires a persona's session only when the persisted authenticated
  state itself becomes unusable between runs (e.g., refresh material
  expired or revoked).
- **FR-006**: Sessions belonging to different personas in the same suite run
  MUST be isolated — no test may observe another persona's session state.
- **FR-007**: The W3C Trace Context (`traceparent` / `tracestate`) assertion
  authored in the existing msal sign-in spec MUST pass once the mechanism is
  in place, confirming UI-originated HTTP requests propagate trace context
  identically whether sign-in was interactive or fixture-seeded.
- **FR-008**: No real human's personal data may appear in any test identity.
  All test identities MUST be obviously synthetic (naming, attributes, owner
  metadata) and clearly distinguishable from production users.
- **FR-009**: Test-user credentials MUST NOT be stored as plain GitHub Actions
  repository secrets. They MUST live in the existing platform's centralized
  secret store and be reached at CI execution time via short-lived federated
  identity — consistent with the constitution's "Managed Identity preferred
  over secrets" rule.
- **FR-010**: No credential, no access token, and no other secret used by the
  mechanism may appear in test output, CI logs, captured traces, screenshots,
  HAR files, or any telemetry the platform emits.
- **FR-011**: The mechanism MUST function locally on a contributor's
  workstation with the same persona declarations the CI run uses, without
  requiring the contributor to retype credentials before each test run.
- **FR-012**: A documented procedure MUST exist for rotating any test-user
  credential. Following it MUST result in the next CI run passing without
  ad-hoc intervention beyond the documented steps.
- **FR-013**: A documented quickstart entry MUST exist explaining how to run
  the authenticated E2E suite locally end-to-end, including how a new
  contributor obtains the required access.
- **FR-014**: If a persona is mis-configured (missing required role, deleted
  identity, expired credential), the mechanism MUST fail with a diagnostic
  pointing at the specific persona — not a generic timeout or sign-in loop.
- **FR-015**: The credential-acquisition flow MUST be browser-automation
  sign-in performed once per persona during the test framework's global
  setup, with the resulting authenticated browser state captured and reused
  by every test that requests that persona. The flow MUST exercise the real
  Microsoft sign-in surface so that the application's authenticated session
  shape stays in sync with the underlying MSAL library across upgrades.
  Direct password-grant exchanges (ROPC) and out-of-band token injection
  that bypasses the IdP UI are explicitly excluded.
- **FR-016**: The four test personas (test identities and any role grants
  they require) MUST be declared in the project's Infrastructure-as-Code
  modules so they are reproduced on every environment build. The IaC scope
  covers: the test identities themselves, any group memberships used to
  carry role assignments, the application role assignments for each persona,
  and any test-only app registration required to broker them.
  Test-user passwords MUST live in the platform's centralized secret store
  (Key Vault), not in IaC state.
- **FR-017**: The mechanism's scope is the browser-side authenticated
  session only. Backend API calls made by the page during a test MUST pick
  up tokens via the application's normal in-app flow — the fixture does not
  intercept or substitute that flow. Direct test-runner-to-API calls that
  bypass the browser are out of scope for this fixture; tests that need
  them must use a separate, explicitly-named helper.

### Key Entities

- **Test Persona**: A named, role-scoped identity the test suite can request.
  At least four exist: Reader, Operator, Admin, zero-role. Each maps to a
  specific test identity in the dev tenant and a specific set of application
  role assignments. The persona name is the only handle a test author uses.
  *(Naming conventions: this spec uses Title-case for readability; the
  canonical machine form — used in code, IaC, schemas, and KV secret names
  — is the lowercase enum literal `reader | operator | admin | none`,
  defined in
  [`contracts/persona-config.schema.json`](./contracts/persona-config.schema.json).)*
- **Test Identity**: A synthetic, dev-tenant-only user account used by exactly
  one persona. Owns no real-person data. Subject to a documented rotation
  procedure.
- **Persona-Annotated Test**: An E2E test that declares which persona it
  requires. The annotation is the contract between the test and the fixture
  mechanism.
- **Authenticated Session Artifact**: The piece of state that, when handed to
  a browser context, causes the application's auth wall to admit the user as
  the chosen persona. Lifetime is bounded by the underlying token validity
  and the fixture's renewal strategy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All twelve currently-suspended (`test.fixme`) authenticated E2E
  test cases listed in the Assumptions section are un-suspended and run
  against the dev environment.
- **SC-002**: After this feature ships, the count of `test.fixme` markers
  attributable to "MSAL E2E auth fixture not available" in the E2E suite is
  zero.
- **SC-003**: A new contributor can go from a clean checkout to running the
  authenticated E2E suite locally without an interactive sign-in in under
  15 minutes using only the documented procedure.
- **SC-004**: On every pull request opened against the default branch, the
  authenticated E2E suite runs to completion and posts a pass/fail status to
  the PR without human intervention.
- **SC-005**: No credential, no access token, and no other secret used by the
  fixture appears in any of: PR status output, CI workflow logs, captured
  Playwright traces, screenshots, HAR files, or platform telemetry, across
  100% of CI runs in a 30-day post-launch window.
- **SC-006**: An operator can rotate any test-user credential in under
  30 minutes using only the documented procedure, and the next CI run after
  rotation passes without ad-hoc intervention beyond the documented steps.
- **SC-007**: The W3C Trace Context (`traceparent` / `tracestate`) assertion
  in the msal sign-in spec passes on every CI run after this feature ships,
  confirming trace propagation is unaffected by fixture-seeded sessions.
- **SC-008**: Persona-scoped session isolation holds across the full suite:
  zero test failures attributable to one persona's session bleeding into
  another's, over a 30-day post-launch window.

## Assumptions

- **Currently-suspended tests in scope.** Eleven Playwright spec files
  collectively contain twelve `test.fixme` cases attributable to the missing
  MSAL auth fixture. The full set this feature will un-suspend is:
  `web/tests/e2e/msal-sign-in-and-whoami.spec.ts` (the sign-in-cycle case
  only — the malformed-bearer 401 case is already live and unaffected),
  `web/tests/e2e/platform-status.spec.ts`,
  `web/tests/e2e/no-access-experience.spec.ts`,
  `web/tests/e2e/role-aware-affordances.spec.ts`, and each of
  `web/tests/e2e/registry/create-browse.e2e.spec.ts`,
  `web/tests/e2e/registry/delete-blocked.e2e.spec.ts`,
  `web/tests/e2e/registry/edit-conflict.e2e.spec.ts`,
  `web/tests/e2e/registry/relationships-audit.e2e.spec.ts`,
  `web/tests/e2e/registry/sc-010-time-to-find.e2e.spec.ts`,
  `web/tests/e2e/registry/search.e2e.spec.ts` (both fixme'd cases), and
  `web/tests/e2e/registry/unauthorized-state.e2e.spec.ts`. The original
  prompt referenced "all 8 specs under `web/tests/e2e/registry/*.e2e.spec.ts`";
  the inventory above resolves to 7 registry spec files containing 8
  fixme'd cases, matching the spirit of the prompt. If a spec file outside
  this list is found to be fixme'd for the same reason, it is included by
  extension.
- **Identity provider.** The existing dev Entra tenant is the only identity
  provider in scope. There is no mock IdP, no B2C/B2B path, and no
  production tenant in scope.
- **Out of scope.** Production identity flows, MFA testing, B2C/B2B flows,
  and conditional-access-policy testing are explicitly excluded. The
  feature targets dev-tenant, password-protected (or equivalent
  non-interactive-credential) identities only.
- **Telemetry posture inherited.** The platform's existing "no PII in
  telemetry by default" rule applies unchanged. Test identities being
  synthetic means there is no real-person PII to leak, but the rule against
  emitting credentials and tokens is treated as a hard requirement here.
- **Trace Context obligation inherited.** The constitution's W3C Trace
  Context propagation requirement on UI-originated HTTP requests applies
  unchanged. The fixture must not bypass or degrade it.
- **Token-lifetime handling.** Resolved in Clarifications: the fixture
  captures each persona's authenticated state once during global setup; the
  in-page client handles access-token refresh silently during a run; the
  fixture re-acquires a persona's state only when the persisted state
  itself becomes unusable between runs. See FR-005 and FR-015.
- **Persona-to-role mapping default.** Reader maps to read-only roles,
  Operator to read+mutate-domain roles, Admin to all current application
  roles including any destructive/administrative ones, and zero-role to an
  authenticated identity with no application role grants. Exact grant
  details will be settled at planning time against the live role catalog,
  but the four-persona shape above is assumed.
- **One dev environment.** The mechanism targets the single existing dev
  environment; multi-environment fan-out (additional staging tenants, for
  example) is not assumed.
- **Existing CI plumbing.** A CI workflow that runs the E2E suite already
  exists. This feature extends that workflow with the credential pathway
  the fixture needs; it does not introduce CI from scratch.
