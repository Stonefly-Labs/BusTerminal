# Agentic coding

> **Spec**: `specs/001-brand-system-and-design-foundation/spec.md` —
> FR-032.
> **Storybook**: `Foundation/08 — Agentic coding`.
> **Constitution**: `.specify/memory/constitution.md` —
> Engineering Workflow & Quality, AI Tooling / MCP usage.

BusTerminal is developed with coding agents — Claude Code, Cursor,
and similar tools — that pair with the project via the **Model
Context Protocol (MCP)**. This document is the contract for how
those tools fit into the project. The short version:

> **MCP servers are development-time tooling for humans and coding
> agents. The product MUST NEVER depend on an MCP server at runtime.**
> (FR-032)

This document is the contributor-facing companion to the Storybook
page; the two stay in sync.

---

## MCP in BusTerminal

The Model Context Protocol gives editor-side agents a typed channel
to consult external sources — docs, APIs, registries, search indices
— while authoring code. The agent calls a tool, receives structured
content, folds it into its next response. None of this touches the
running product.

| Server | Used at edit time for |
|---|---|
| `context7` | Library docs — React, Next.js, Tailwind, Zod, TanStack Table, RHF, Recharts. Preferred over web search for library questions. |
| `microsoft-learn` | Official Microsoft / Azure docs — Service Bus, App Insights, Container Apps, OpenTelemetry, Entra ID. |
| `next-devtools` | Next.js docs + App Router introspection. |
| `shadcn` | Component-registry lookups when adding or auditing a primitive. |

None of these servers appear in `package.json`. None of them are
imported from `web/lib/**`, `web/components/**`, or any production
code path. They are configured in the agent's environment, not the
product's.

---

## The rule, in framing

Phrase docs and code as *"coding agents must consult X at edit
time,"* never *"BusTerminal integrates with X."*

Conforming phrasing:

- *"Coding agents must consult `context7` for current Next.js docs
  before authoring routing changes."*
- *"When adding a primitive, the agent should query the `shadcn` MCP
  to confirm the registry contract."*
- *"`microsoft-learn` is the canonical source for Service Bus API
  documentation at edit time."*

Non-conforming phrasing (these are defects):

- *"BusTerminal queries the Microsoft Learn MCP server to fetch
  Service Bus docs inline."*
- *"Add a route that proxies an MCP so the UI shows Azure docs
  inline."*
- *"Use the shadcn MCP at runtime to hot-load primitives."*

---

## Anti-patterns

- A runtime fetch from a frontend module to an MCP endpoint.
- An import of an MCP client library in `web/lib/**` or
  `web/components/**`.
- A scheduled production job that calls an MCP tool.
- A feature framed as "BusTerminal queries the MCP" even if it
  technically doesn't — the framing leaks the dev-time tool into the
  product surface and confuses future readers.

---

## When to consult an MCP server vs. when to read the code

Consult an MCP server when:

- The question is about a **library's current API surface** —
  signatures, options, deprecations, migration notes.
- The question is about **official Microsoft / Azure docs** — Service
  Bus semantics, OpenTelemetry attributes, Entra ID flows, Container
  Apps configuration.
- The question is about a **registry-owned artifact** — the shape of
  a shadcn registry entry, the schema of a `manifest.json`.

Read the project code when:

- The question is about **BusTerminal's own conventions** — primitive
  layout, token names, file structure. The constitution and the
  foundation specs are authoritative here.
- The question is about the **foundation contract** — read the
  contracts under
  `specs/001-brand-system-and-design-foundation/contracts/` and the
  spec itself.
- The question is about a **specific behavior of a primitive** — read
  the primitive source and its stories. Stories are authoritative.

Don't ask an MCP server about BusTerminal-internal conventions —
they don't know. The project is the source of truth for itself.

---

## Project conventions the agent should already follow

Encoded in `CLAUDE.md`, but worth repeating:

- **Spec-Driven Development.** Significant changes flow through
  `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` →
  `/speckit-tasks` → `/speckit-implement`.
- **Constitution wins.** When spec or doc conflicts with the
  constitution, the constitution is correct and the doc is out of
  date.
- **App Router only**, not Pages Router.
- **No second design system.** shadcn/ui (project-owned) is the
  baseline.
- **No additional UI library** without an ADR — no alternative
  component libraries, heavy chart suites, graph/topology libraries,
  drag-and-drop libraries, rich-text editors, code-editor
  components.
- **OpenTofu, not Bicep.** Bicep requires an ADR exception.
- **Minimal APIs, not Controllers**, in any future backend work.
- **Managed Identity over secrets** for service-to-service auth.
- **W3C Trace Context propagation** is mandatory on every UI-originated
  HTTP request — agents must not propose stripping `traceparent` for
  any reason.
- **No PII in telemetry by default** — only correlation identifiers.

---

## Related documents

- `web/stories/08-agentic-coding.mdx` — Storybook companion.
- `.specify/memory/constitution.md` — the governing document.
- `CLAUDE.md` at the repo root — project context for coding agents.
- `specs/001-brand-system-and-design-foundation/spec.md` — FR-032
  for the runtime-dependency prohibition.
