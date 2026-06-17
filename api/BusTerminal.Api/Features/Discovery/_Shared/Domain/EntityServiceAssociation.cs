namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §1.1 + §3. M:N entity↔service link denormalized
// inside each PublishedEntity document. AssociationId is `esa_` + ULID; the
// (entityId, serviceId, role) triple is the natural uniqueness key — enforced
// by the AddAssociationEndpoint validator on POST.
public sealed record EntityServiceAssociation(
    string AssociationId,
    string ServiceId,
    EntityServiceRole Role,
    DateTimeOffset CreatedUtc,
    string CreatedBy);
