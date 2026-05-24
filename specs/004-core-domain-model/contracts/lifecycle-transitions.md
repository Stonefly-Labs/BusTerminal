# Lifecycle Transitions

The canonical model enforces a **strict forward-only progression with a single Draft edit loop** (Q1 clarification, FR-010). The transition graph is implemented in `BusTerminal.Api/Domain/LifecycleTransitions.cs` and exercised by `LifecycleTransitionsTests`.

---

## Legal transitions

```text
   ┌─────────┐
   │  Draft  │◄─┐    Free edits while in Draft (Draft → Draft is a no-op)
   └────┬────┘  │
        │       │
        ▼       │
   ┌─────────┐  │
   │ Active  │──┘    ILLEGAL: Active → Draft (cannot revert to Draft once made Active)
   └────┬────┘
        │
        ▼
   ┌──────────┐
   │Deprecated│◄─┐   Un-deprecate is permitted (Deprecated → Active)
   └────┬─────┘  │
        │  ▲     │
        │  └─────┘
        ▼
   ┌─────────┐
   │ Retired │       Terminal-forward: only Retired → Archived
   └────┬────┘
        │
        ▼
   ┌─────────┐
   │Archived │       Terminal: no backward transitions
   └─────────┘
```

| From | To | Legal? |
|---|---|---|
| Draft | Draft | ✅ (free edits) |
| Draft | Active | ✅ |
| Draft | Deprecated | ❌ Error |
| Draft | Retired | ❌ Error |
| Draft | Archived | ❌ Error |
| Active | Draft | ❌ Error — once Active, the resource cannot revert to Draft. Create a successor. |
| Active | Active | ⚪ no-op (updates that don't change lifecycle) |
| Active | Deprecated | ✅ |
| Active | Retired | ❌ Error — must transit through Deprecated. |
| Active | Archived | ❌ Error — must transit through Deprecated → Retired. |
| Deprecated | Draft | ❌ Error |
| Deprecated | Active | ✅ (un-deprecate) |
| Deprecated | Retired | ✅ |
| Deprecated | Archived | ❌ Error — must transit through Retired. |
| Retired | Draft | ❌ Error |
| Retired | Active | ❌ Error |
| Retired | Deprecated | ❌ Error |
| Retired | Retired | ⚪ no-op |
| Retired | Archived | ✅ |
| Archived | * (any) | ❌ Error — terminal. |

Illegal transitions are rejected by `LifecycleTransitionRule` with severity **Error**, blocking the write. The error includes the from/to states and the resource id.

---

## Replacement of Retired or Archived resources

A Retired or Archived resource cannot be "revived." Replacement requires creating a **successor resource** with:

1. Its own identifier (FR-021).
2. Its own version lineage (`Version.VersionHistory` references the predecessor).
3. An optional `replaces` relationship pointing back at the predecessor (recommended for graph traversal).

---

## Soft-delete and restoration are NOT lifecycle transitions

Soft-delete (FR-020) is orthogonal to lifecycle state. A resource in any lifecycle state can be soft-deleted; the `isDeleted` flag is set independently. **Restoration of a soft-deleted resource returns it to its prior lifecycle state without going through the legal-transition rules above** — restoration is a distinct operation, not a lifecycle transition.

Both soft-delete and restoration emit a `softDeleted` / `restored` event into the change-event log (per Q5 / FR-015).
