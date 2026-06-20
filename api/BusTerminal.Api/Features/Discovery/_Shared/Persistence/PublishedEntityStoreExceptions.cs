namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T109. Typed exception surface for the published-entity write
// path. Endpoints catch these and map to HTTP semantics:
//   PublishedEntityNotFoundException        → 404
//   PublishedEntityConcurrencyConflictException → 412
//   DuplicateServiceAssociationException    → 409
//   ServiceAssociationNotFoundException     → 404
// The store is intentionally HTTP-agnostic; the mappings live in the
// endpoint handlers so unit tests can exercise the store in isolation.

public sealed class PublishedEntityNotFoundException : Exception
{
    public PublishedEntityNotFoundException(string entityId, string environment)
        : base($"Published entity '{entityId}' not found in environment '{environment}'.")
    {
        EntityId = entityId;
        Environment = environment;
    }

    public string EntityId { get; }
    public string Environment { get; }
}

public sealed class PublishedEntityConcurrencyConflictException : Exception
{
    public PublishedEntityConcurrencyConflictException(string entityId, string presentedEtag, Exception? innerException = null)
        : base($"Concurrency conflict on published entity '{entityId}'. Presented ETag '{presentedEtag}' is stale.", innerException)
    {
        EntityId = entityId;
        PresentedEtag = presentedEtag;
    }

    public string EntityId { get; }
    public string PresentedEtag { get; }
}

public sealed class DuplicateServiceAssociationException : Exception
{
    public DuplicateServiceAssociationException(string entityId, string serviceId, string role)
        : base($"Service association ({serviceId}, {role}) already exists on entity '{entityId}'.")
    {
        EntityId = entityId;
        ServiceId = serviceId;
        Role = role;
    }

    public string EntityId { get; }
    public string ServiceId { get; }
    public string Role { get; }
}

public sealed class ServiceAssociationNotFoundException : Exception
{
    public ServiceAssociationNotFoundException(string entityId, string associationId)
        : base($"Association '{associationId}' not found on entity '{entityId}'.")
    {
        EntityId = entityId;
        AssociationId = associationId;
    }

    public string EntityId { get; }
    public string AssociationId { get; }
}
