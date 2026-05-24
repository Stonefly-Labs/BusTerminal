namespace BusTerminal.Api.Domain.Validation;

// Spec 004 / Q3 + FR-013.
//
// A validation rule examines a Resource (optionally with surrounding context — a
// relationship resolver, a duplicate-detector, IServiceProvider for rule-internal
// deps) and yields zero or more findings. Rules are pure-CPU; no I/O is allowed in
// Validate() itself.
public interface IValidationRule
{
    // Optional applicability predicate. Returning true (the default for the
    // extension method below) means "this rule fires for every resource type."
    bool AppliesTo(Type resourceType) => true;

    IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context);
}

// Relationship resolution + duplicate detection are passed in via callbacks rather
// than as interfaces because (a) the callers are pure-functional rules, (b) it
// avoids forcing every rule to know about Cosmos/persistence, (c) it keeps the
// validation pass cache-friendly (the engine warms a single relationship cache once
// per pass).
public sealed class ValidationContext
{
    public required Func<ResourceId, Resource?> RelationshipResolver { get; init; }

    public required Func<Resource, bool> DuplicateDetector { get; init; }

    public required IServiceProvider Services { get; init; }

    // Set by the persistence layer (on Update) so LifecycleTransitionRule can
    // compare against the stored state. Null on Create.
    public LifecycleState? PreviousLifecycle { get; init; }
}
