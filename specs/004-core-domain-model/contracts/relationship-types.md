# Relationship Type Vocabulary

The canonical model's relationship graph (FR-008) uses a **closed enum** of relationship types in v1. The validator enforces source/target resource-type pairing per the table below — invalid pairings emit an `Error`-severity finding.

Adding a new relationship type is a future-slice operation that extends the enum + the source/target pairing table. Existing relationships with retired types should never occur in v1 (the enum is closed), but the deserializer accepts an unknown type and emits an `Info` finding to preserve forward compatibility.

---

## Vocabulary

| Type | Source resource type(s) | Target resource type(s) | Direction | Description |
|---|---|---|---|---|
| `publishesTo` | `producerApplication` | `queue`, `topic` | one-way | Producer publishes messages to the target. |
| `consumedBy` | `queue`, `subscription` | `consumerApplication` | one-way | Target consumes messages from the source. |
| `subscriptionOf` | `subscription` | `topic` | one-way | Parent-child relationship between a subscription and its topic. Equivalent to `Subscription.parentTopicId` but represented in the graph for traversal. |
| `usesContract` | `queue`, `topic` | `messageContract` | one-way | The source carries messages whose shape conforms to the target contract. |
| `owns` | `team` | any operational type (`broker`, `queue`, `topic`, `subscription`, `messageContract`, `producerApplication`, `consumerApplication`, `integrationFlow`) | one-way | Team owns the target. Equivalent to `OwnershipRecord.owningTeamId` but represented in the graph for traversal. |
| `attachedTo` | `documentationAsset` | any | one-way | Documentation asset documents the target. Equivalent to `DocumentationAsset.attachedResourceIds` but represented for traversal. |
| `replaces` | any | any (same resource type) | one-way | Successor relationship after a resource is Retired or Archived (Q1). The successor is the source; the predecessor is the target. |
| `partOfFlow` | `producerApplication`, `consumerApplication`, `queue`, `topic` | `integrationFlow` | one-way | The source participates in the named integration flow. |

---

## Validation rules per type

For each type, the validator asserts:

1. **Source resolves**: the `sourceId` resolves to an existing resource (or a soft-deleted resource — Warning, not Error, per Edge Case "Dangling references after soft-delete").
2. **Target resolves**: same as above.
3. **Source type matches the allowed source set** for the relationship type.
4. **Target type matches the allowed target set** for the relationship type.
5. **No self-relationship** (`sourceId == targetId`) unless explicitly permitted by the type. None of the v1 types permit self-relationships; violations emit Error.
6. **Cycle protection** during traversal: the graph may contain cycles (legal — see Edge Case "Circular relationships"), but the traversal helper (`RelationshipGraph.cs`) terminates by tracking visited nodes.

---

## Forward compatibility

- Adding a new relationship type: future slice extends the enum and the table. Existing data is unaffected.
- Renaming an existing type: prohibited without a coordinated migration (every persisted relationship document carries the type string). Treat as a breaking change requiring an ADR.
- Removing a type: prohibited without a coordinated migration. Treat the same as a contract version retirement.
