namespace BusTerminal.Api.Domain;

// Spec 004 — in-document reference to a first-class TagResource. The "Reference"
// suffix is intentional to distinguish from TagResource (the catalog entry).
// Per analysis finding N1 — rename Tag → TagReference so the reference-vs-resource
// distinction is visible at use sites.
public readonly record struct TagReference(ResourceId TagId, string Name);
