namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.3 OwnershipAssignment +
// contracts/ownership-assignment.schema.json. Structured Entra-backed
// ownership reference. `displayNameSnapshot` is captured at assignment time
// so the details panel can render a meaningful name even when Graph
// re-resolution fails (FR-011 fallback).
public sealed record OwnershipAssignment(
    OwnershipRole Role,
    PrincipalType PrincipalType,
    Guid ObjectId,
    string DisplayNameSnapshot,
    DateTimeOffset AssignedAtUtc,
    Guid AssignedBy);
