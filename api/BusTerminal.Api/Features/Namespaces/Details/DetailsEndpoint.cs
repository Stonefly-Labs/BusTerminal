using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Details;

// Spec 008 / T103 / US2. GET /api/namespaces/{id}. Joined response:
//   - All OnboardedNamespace fields (NamespaceResponse via NamespaceDtoMapping).
//   - latestValidationRun (from namespace-validation-runs via
//     INamespaceValidationRunStore.GetAsync(id, lastValidationRunId)).
//   - recentAuditEvents (from registry-audit via
//     IAuditEventStore.ListForEntityAsync(id, limit=20)).
//
// AuthN-only — any authenticated tenant user can view details.
public static class DetailsEndpoint
{
    private const int RecentAuditLimit = 20;

    public static IEndpointRouteBuilder MapDetailsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/namespaces/{id:guid}", HandleAsync)
            .RequireAuthorization()
            .WithName("NamespaceDetailsGet")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        IRegistryEntityStore entityStore,
        INamespaceValidationRunStore runStore,
        IAuditEventStore auditStore,
        NamespaceDtoMapping mapping,
        CancellationToken cancellationToken)
    {
        var entity = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is not RegistryNamespace ns || ns.Source != RegistrySource.Onboarded)
        {
            return Results.Problem(
                title: "NotFound",
                detail: $"No onboarded namespace with id {id:D} was found.",
                statusCode: StatusCodes.Status404NotFound,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["code"] = "NotFound",
                });
        }

        ValidationRun? latestRun = null;
        if (ns.LastValidationRunId is Guid runId)
        {
            latestRun = await runStore.GetAsync(ns.Id, runId, cancellationToken).ConfigureAwait(false);
        }

        var recentAudit = await auditStore
            .ListForEntityAsync(ns.Id, RecentAuditLimit, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(ns.Etag))
        {
            context.Response.Headers.ETag = ns.Etag;
        }

        var baseDto = mapping.ToResponse(ns);
        var response = new NamespaceDetailsResponse(
            baseDto.Id,
            baseDto.EntityType,
            baseDto.Source,
            baseDto.Name,
            baseDto.FullyQualifiedName,
            baseDto.DisplayName,
            baseDto.Description,
            baseDto.Environment,
            baseDto.Status,
            baseDto.AzureResourceId,
            baseDto.SubscriptionId,
            baseDto.SubscriptionName,
            baseDto.ResourceGroup,
            baseDto.TenantId,
            baseDto.Region,
            baseDto.BusinessUnit,
            baseDto.ProductOrApplication,
            baseDto.CostCenter,
            baseDto.Notes,
            baseDto.Tags,
            baseDto.LifecycleStatus,
            baseDto.ValidationStatus,
            baseDto.LastValidationRunId,
            baseDto.LastValidatedAtUtc,
            baseDto.Ownership,
            baseDto.OnboardingActor,
            baseDto.CreatedAtUtc,
            baseDto.UpdatedAtUtc,
            baseDto.Etag,
            latestRun,
            recentAudit);

        return Results.Json(response, statusCode: StatusCodes.Status200OK);
    }
}

// Spec 008 / contracts/namespace-onboarding-api.yaml#/OnboardedNamespaceDetails.
//
// Flat extension of NamespaceResponse (allOf composition in the OpenAPI
// contract) — all namespace fields at the top level plus `latestValidationRun`
// and `recentAuditEvents` siblings.
public sealed record NamespaceDetailsResponse(
    Guid Id,
    string EntityType,
    string Source,
    string Name,
    string? FullyQualifiedName,
    string? DisplayName,
    string? Description,
    string Environment,
    string Status,
    string? AzureResourceId,
    Guid? SubscriptionId,
    string? SubscriptionName,
    string? ResourceGroup,
    Guid? TenantId,
    string? Region,
    string? BusinessUnit,
    string? ProductOrApplication,
    string? CostCenter,
    string? Notes,
    IReadOnlyList<RegistryTag> Tags,
    string? LifecycleStatus,
    string? ValidationStatus,
    Guid? LastValidationRunId,
    DateTimeOffset? LastValidatedAtUtc,
    OwnershipBlock? Ownership,
    OnboardingActor? OnboardingActor,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? Etag,
    ValidationRun? LatestValidationRun,
    IReadOnlyList<AuditEvent> RecentAuditEvents);
