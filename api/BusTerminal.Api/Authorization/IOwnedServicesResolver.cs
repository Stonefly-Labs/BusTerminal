using System.Security.Claims;

namespace BusTerminal.Api.Authorization;

// Spec 009 / T023 + R-15. Resolves the set of service IDs the authenticated
// caller has Service Owner standing for. Used by EntityMetadataEditorAuthorizer
// (the third branch of R-15's three-way edit decision) and by the frontend's
// useOwnedServices hook via a (future) /api/me/owned-services endpoint.
//
// Phase 2 ships a NoOpOwnedServicesResolver returning the empty set —
// downstream specs (or a follow-up to spec 003) wire a real source. The
// abstraction lives here so the authorizer's third branch is implementable
// today without forcing the rest of Phase 2 to wait on spec 003 changes.
public interface IOwnedServicesResolver
{
    Task<IReadOnlySet<string>> GetOwnedServiceIdsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}

public sealed class NoOpOwnedServicesResolver : IOwnedServicesResolver
{
    private static readonly IReadOnlySet<string> Empty = new HashSet<string>(0);
    public Task<IReadOnlySet<string>> GetOwnedServiceIdsAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken) => Task.FromResult(Empty);
}
