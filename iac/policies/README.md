# BusTerminal IaC policy gate

Custom rule set (bash + jq) that runs against `tofu show -json tfplan`
output to enforce the constitution + spec-005 invariants the built-in
tools (`tofu validate`, `checkov`, `tfsec`) don't cover. Implementation of
spec **005-infrastructure-baseline** FR-044.

The binding rule definitions live in
[`specs/005-infrastructure-baseline/contracts/policy-rules.md`](../../specs/005-infrastructure-baseline/contracts/policy-rules.md).
This README is operational: how to run the gate, what each rule does, and
how to use the allowlist.

---

## Why bash + jq

Per research §16, bash + jq is already on every GitHub-hosted runner and
in this repo's existing scripts. Adopting OPA/Conftest (Rego) for one
spec's worth of checks would add a toolchain without a proportional
benefit. If the rule set ever grows complex enough to warrant Rego, a
later spec can refactor — the orchestrator's exit-code and JSON-report
contracts are stable enough to swap implementations.

---

## Files

| File | Purpose |
|---|---|
| `run-policies.sh` | Orchestrator. Runs every rule, accumulates failures, emits Markdown summary to stdout + JSON detail to `--report`. Exit code 0/1/2 per the contract. |
| `check-tags.sh` | **BT-IAC-001** — Mandatory tag coverage on every taggable resource. |
| `check-public-access.sh` | **BT-IAC-002** — No public-network-access data services in prod (env-conditional). |
| `check-diagnostics.sh` | **BT-IAC-003** — Diagnostic-settings coverage + shape (`allLogs` only, no metrics). |
| `check-rbac-scope.sh` | **BT-IAC-004** — Workload UAMIs must not receive subscription-wide or management-plane grants. |
| `check-outputs-no-secrets.sh` | **BT-IAC-005** — Non-sensitive outputs must not leak secrets; App Insights connection string MUST be sensitive. |
| `check-lockfile.sh` | **BT-IAC-006** — `.terraform.lock.hcl` is committed and matches `tofu init -upgrade=false`. |
| `check-stateful-destroys.sh` | **BT-IAC-007** — No `delete` / `destroy-replace` on stateful resources without explicit approval. |
| `allowlist.json` | Per-rule allowlist with required `justification` strings. |

---

## Exit codes

| Code | Meaning |
|---|---|
| `0` | All rules passed. |
| `1` | One or more rule failures. CI must block merge. |
| `2` | Setup error — missing tfplan, `jq` not on PATH, bad arguments. CI must fail loudly; this is never a "rule passed" result. |

Individual `check-*.sh` scripts honor the same exit-code contract so they
can be invoked standalone during local debugging.

---

## How to run locally

```bash
cd iac/environments/dev
tofu init
tofu plan -var-file=terraform.tfvars -out=tfplan
tofu show -json tfplan > tfplan.json

bash ../../policies/run-policies.sh \
  --plan tfplan.json \
  --env dev \
  --allowlist ../../policies/allowlist.json \
  --composition-dir . \
  --report policies-report.json
```

The Markdown summary lands on stdout (suitable for piping to `gh pr
comment --body-file -`); the structured JSON report is written to
`policies-report.json` for inspection or downstream tooling. If
`BT-IAC-007` fires, a `requires-manual-approval.flag` file is written
next to the report so a CI job can detect the destructive change and
gate apply on a reviewer approval.

`--composition-dir` is only used by `BT-IAC-006`. The other rules read
only the tfplan JSON.

---

## Rule reference

The full normative text — including the exact resource types each rule
inspects, the precise failure-message format, and the allowlist key
format — is in [`policy-rules.md`](../../specs/005-infrastructure-baseline/contracts/policy-rules.md).
Quick-reference table:

| Rule | Asserts | Allowlist allowed? |
|---|---|---|
| `BT-IAC-001` | Every taggable resource carries the 5 mandatory tags (`application`, `environment`, `managed-by`, `cost-center`, plus `owner` OR `team`). | Yes — by resource address. |
| `BT-IAC-002` | In prod environments, the listed data services have `public_network_access_enabled = false`. | Yes — rare; prod public access requires reviewer escalation. |
| `BT-IAC-003` | Every supported resource has a diagnostic setting forwarding `allLogs` to LAW; no `enabled_metric` blocks. | Yes — for resources Azure does not support diagnostic settings on. |
| `BT-IAC-004` | Workload UAMIs never receive subscription-wide or management-plane grants. | Yes — pre-seeded with the documented pipeline-MI exceptions. |
| `BT-IAC-005` | Non-sensitive outputs do not match secret patterns; App Insights connection string IS marked sensitive. | **No.** |
| `BT-IAC-006` | `.terraform.lock.hcl` is committed and matches a clean `tofu init -upgrade=false` resolution. | **No.** |
| `BT-IAC-007` | No `delete` or `destroy-replace` action targets any stateful resource. Triggers the manual-approval banner. | Yes — by resource address; reviewers must enforce the required justification in the PR description. |

---

## Allowlist conventions

`allowlist.json` is a top-level object keyed by rule ID, with each entry
shaped per the rule. The repository convention:

1. **Each allowlist entry MUST carry a `justification` string.** It is
   not parsed by the gate — it is a contract for reviewers and a paper
   trail for future operators reading `git blame`.
2. **Allowlist edits require a reviewer sign-off PR.** No "while I'm in
   here" allowlist additions.
3. **Prefer fixing the violation over adding an allowlist entry.** The
   pre-seeded BT-IAC-004 entries for the pipeline MI are the only
   currently-justified exceptions (per the Complexity Tracking section
   of `specs/005-infrastructure-baseline/plan.md`).
4. **The orchestrator records consumed allowlist entries in its JSON
   report.** Reviewers see what was bypassed even on otherwise-green
   checks.

The supported per-rule entry shapes are documented in
[`policy-rules.md` §`Allowlist file format`](../../specs/005-infrastructure-baseline/contracts/policy-rules.md#allowlist-file-format).

---

## Heuristics + known limitations

The rules use practical heuristics where literal tfplan introspection
isn't feasible at plan time:

- **`BT-IAC-003` coverage check** matches by module-path proximity, not
  by literal `target_resource_id` correlation (the target ID is usually
  `(known after apply)` for net-new resources). Each module that needs
  diagnostic coverage instantiates the `iac/modules/diagnostic-settings`
  sub-module under itself — the coverage check verifies that pattern.
- **`BT-IAC-004` principal identification** uses the resource-address
  convention (`*workload*` ↔ workload UAMI, `*pipeline*` ↔ pipeline UAMI)
  rather than resolving post-apply UUIDs. If the convention is broken in
  a future module, update both the module address pattern and this rule.
- **`BT-IAC-006` lockfile drift** is checked by re-running `tofu init
  -backend=false -upgrade=false` in a temp copy of the composition and
  comparing SHA-256 of the resulting `.terraform.lock.hcl`. Requires
  `tofu` on PATH; mirrors what CI runners have.

If a rule's heuristic produces a false-positive in your specific case,
the right move is usually to (a) make the code clearer to the rule, or
(b) add a justified allowlist entry — not to weaken the rule.
