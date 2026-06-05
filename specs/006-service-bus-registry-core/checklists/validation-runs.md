# Spec 006 — Validation Runs Checklist

**Status**: scaffold for the PR-merge validation gate. Populate the boxes below before merging the spec-006 PR.

This file consolidates the **environment-dependent** Phase 6 polish tasks that cannot be completed by a coding agent — they require a running dev environment, deployed services, App Insights traffic, and (for IaC) the pipeline-supplied tfvars. The code/test artifacts they validate are already in place; this file is the record that the validation was performed.

## T128 — Perf validation (SC-002/SC-003/SC-004/SC-005)

- [ ] Captured search p95 (< 1s)
- [ ] Captured detail page load p95 (< 500ms)
- [ ] Captured CRUD API p95 (< 1s)
- [ ] Captured indexer lag p95 (< 5s)
- [ ] Recorded in [`perf-results.md`](./perf-results.md)

## T129 — Accessibility validation (SC-008)

- [ ] `pnpm test:a11y` reports zero violations on **all** registry routes (explorer, search, detail, reduced-motion) on **both** dark and light themes

Run against the dev environment (or a locally-running dev server with the registry seeded via the quickstart). Capture the Playwright HTML report URL in the PR description.

## T130 — Trace correlation validation (SC-012)

- [ ] Selected an arbitrary UI trace in App Insights against the dev environment
- [ ] Confirmed the linked backend spans share the same `traceId`
- [ ] Captured a screenshot or App Insights link in the PR description

Suggested KQL:

```kusto
union dependencies, requests
| where cloud_RoleName in ("busterminal-web", "BusTerminal.Api")
| where timestamp > ago(1h)
| project timestamp, cloud_RoleName, name, operation_Id, operation_ParentId
| order by operation_Id, timestamp asc
```

Pick any `operation_Id` and confirm rows from both roles appear with consistent parenting.

## T133 — BT-IAC policy gate (zero violations)

- [ ] CI run of `iac/policies/run-policies.sh` against the spec-006 plan reports zero failures
- [ ] No new allowlist entries in `iac/policies/allowlist.json`
- [ ] Markdown summary attached to the PR description

The policy script requires the pipeline-supplied tfvars (subscription_id, entra_*, image refs, github_org_repo, unique_suffix) and a current `tofu plan` JSON, so this runs under CI on the PR — not locally. Confirm the CI job logs show:

```
✅ BT-IAC-001..007 — all rules passed against environment dev
```

## Notes

- Each item is operator-side (or CI-side) because the corresponding signal is generated only by deployed services / a CI-attached `tofu plan`.
- The code artifacts these validate (registry endpoints, indexer, a11y test suite, IaC modules, policy gates) are all in place — see `tasks.md` Phases 1–5 and the Phase-6 code tasks (T126, T127, T129a, T129b, T131, T132, T134) which are completed by the spec-006 implementation.
