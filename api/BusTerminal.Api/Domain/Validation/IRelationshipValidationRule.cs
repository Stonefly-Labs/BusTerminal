using BusTerminal.Api.Domain.Relationships;

namespace BusTerminal.Api.Domain.Validation;

// Spec 004 / T107. Parallel of IValidationRule for the Relationship peer
// document. Kept as a distinct interface because Relationship is intentionally
// NOT a Resource subtype (T102) — a single rule type would have to accept
// `object` or split with `if` on every call site.
//
// Rules are pure-CPU; no I/O is allowed in Validate() itself. Relationship
// resolution + duplicate detection share the same ValidationContext as the
// Resource pipeline so a single per-pass resolver cache serves both.
public interface IRelationshipValidationRule
{
    IEnumerable<ValidationFinding> Validate(Relationship relationship, ValidationContext context);
}
