using System.Security.Claims;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Authorization;

// Spec 009 / T023 + R-15. Runtime authorization helper for the PATCH
// /api/entities/{id} family of endpoints. Replaces a static ASP.NET policy
// because the third branch (ServiceOwner-of-Owner-association) needs the
// entity itself to be resolved before the decision can be made.
//
// Decision order:
//   1. Caller holds BusTerminal.Admin              → allow (via=admin)
//   2. Caller holds BusTerminal.NamespaceAdministrator → allow (via=namespaceAdmin)
//   3. Any of caller's owned services appears as an Owner-role association
//      on the entity                               → allow (via=serviceOwner)
//   4. Otherwise                                   → deny
//
// The disposition + branch is logged structurally for audit (no PII —
// only the entity id + decision label).
public sealed partial class EntityMetadataEditorAuthorizer
{
    private readonly IOwnedServicesResolver _ownedServices;
    private readonly ILogger<EntityMetadataEditorAuthorizer> _logger;

    public EntityMetadataEditorAuthorizer(
        IOwnedServicesResolver ownedServices,
        ILogger<EntityMetadataEditorAuthorizer> logger)
    {
        _ownedServices = ownedServices;
        _logger = logger;
    }

    public async Task<EntityEditAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal principal,
        string entityId,
        IReadOnlyList<EntityServiceAssociation> serviceAssociations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentNullException.ThrowIfNull(serviceAssociations);

        var roles = principal.GetEffectiveRoles(_logger);
        if (roles.Contains(PlatformRole.Admin))
        {
            LogAllow(entityId, "admin");
            return EntityEditAuthorizationResult.Allow(EntityEditAuthorizationVia.Admin);
        }
        if (roles.Contains(PlatformRole.NamespaceAdministrator))
        {
            LogAllow(entityId, "namespaceAdmin");
            return EntityEditAuthorizationResult.Allow(EntityEditAuthorizationVia.NamespaceAdmin);
        }

        var owned = await _ownedServices.GetOwnedServiceIdsAsync(principal, cancellationToken).ConfigureAwait(false);
        if (owned.Count == 0)
        {
            LogDeny(entityId, "no-owned-services");
            return EntityEditAuthorizationResult.Deny();
        }

        var ownerAssoc = serviceAssociations.Any(a =>
            a.Role == EntityServiceRole.Owner && owned.Contains(a.ServiceId));
        if (ownerAssoc)
        {
            LogAllow(entityId, "serviceOwner");
            return EntityEditAuthorizationResult.Allow(EntityEditAuthorizationVia.ServiceOwner);
        }

        LogDeny(entityId, "no-owner-association");
        return EntityEditAuthorizationResult.Deny();
    }

    [LoggerMessage(EventId = 9501, Level = LogLevel.Information, Message = "authz.decision=allow entity={EntityId} via={Via}")]
    private partial void LogAllow(string entityId, string via);

    [LoggerMessage(EventId = 9502, Level = LogLevel.Information, Message = "authz.decision=deny entity={EntityId} reason={Reason}")]
    private partial void LogDeny(string entityId, string reason);
}

public enum EntityEditAuthorizationVia { Admin, NamespaceAdmin, ServiceOwner }

public readonly record struct EntityEditAuthorizationResult(bool Allowed, EntityEditAuthorizationVia? Via)
{
    public static EntityEditAuthorizationResult Allow(EntityEditAuthorizationVia via) => new(true, via);
    public static EntityEditAuthorizationResult Deny() => new(false, null);
}
