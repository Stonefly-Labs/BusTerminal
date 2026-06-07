namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-020 / research §8. Domain exception raised by
// CosmosRegistryEntityStore when Cosmos returns 412 PreconditionFailed on an
// IfMatch write. The endpoint layer (T078) catches this and runs
// ConcurrencyConflictMapper (T038) to produce the wire-shape ConflictResponse.
//
// Distinct from spec-004's ConcurrencyConflictException because registry
// entities use raw Guid ids (not the canonical-domain ResourceId type) and the
// 409 response shape includes registry-specific extension members.
public sealed class RegistryConcurrencyConflictException : Exception
{
    public RegistryConcurrencyConflictException(
        Guid entityId,
        string presentedEtag,
        string? currentEtag,
        Exception? innerException = null)
        : base(
            $"Concurrency conflict on registry entity {entityId}. " +
            $"Presented ETag: '{presentedEtag}', current: '{currentEtag ?? "<unknown>"}'.",
            innerException)
    {
        EntityId = entityId;
        PresentedEtag = presentedEtag;
        CurrentEtag = currentEtag;
    }

    public Guid EntityId { get; }
    public string PresentedEtag { get; }
    public string? CurrentEtag { get; }
}
