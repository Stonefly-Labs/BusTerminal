using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Namespaces.Ownership;

// Spec 008 / T129 + FR-010 + contracts/namespace-onboarding-api.yaml#/UpdateOwnershipRequest.
// PUT /api/namespaces/{id}/ownership — full-block replace.
//
// Pipeline mirrors UpdateMetadataEndpoint:
//   1. namespace-administrator role gate.
//   2. If-Match required.
//   3. 404 when missing or not source=Onboarded.
//   4. FluentValidation via UpdateOwnershipValidator (exactly-one PrimaryOwner,
//      no duplicate (role, objectId), valid Guids per FR-010).
//   5. `with`-update the Ownership block (other fields untouched).
//   6. UpdateAsync with If-Match concurrency.
//   7. Write NamespaceOwnershipUpdated audit event with per-role diff.
//   8. Return 200 + new ETag.
public static class UpdateOwnershipEndpoint
{
    public static IEndpointRouteBuilder MapUpdateOwnershipEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPut("/api/namespaces/{id:guid}/ownership", HandleAsync)
            .RequireAuthorization()
            .RequireNamespaceAdministrator()
            .WithName("NamespaceOwnershipUpdate")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        UpdateOwnershipRequest? request,
        IRegistryEntityStore entityStore,
        IAuditEventStore auditStore,
        UpdateOwnershipValidator validator,
        ConcurrencyConflictMapper conflictMapper,
        NamespaceDtoMapping mapping,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues)
            || string.IsNullOrEmpty(ifMatchValues.ToString()))
        {
            return Problem(StatusCodes.Status428PreconditionRequired, "IfMatchRequired",
                "PUT requires the If-Match header carrying the entity's current ETag.",
                context.Request.Path);
        }
        var ifMatchEtag = ifMatchValues.ToString();

        if (request is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "EmptyBody",
                "Request body is required.", context.Request.Path);
        }
        request = request with { Id = id };

        var current = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not RegistryNamespace ns || ns.Source != RegistrySource.Onboarded)
        {
            return Problem(StatusCodes.Status404NotFound, "NotFound",
                $"No onboarded namespace with id {id:D} was found.", context.Request.Path);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                instance: context.Request.Path,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var now = timeProvider.GetUtcNow();
        var updatedEntity = ns with { UpdatedAtUtc = now } with { Ownership = request.Ownership };

        RegistryEntity persisted;
        try
        {
            persisted = await entityStore.UpdateAsync(updatedEntity, ifMatchEtag, cancellationToken).ConfigureAwait(false);
        }
        catch (RegistryConcurrencyConflictException)
        {
            var fresh = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? ns;
            var conflict = conflictMapper.BuildConflict(fresh, updatedEntity, ifMatchEtag, context.Request.Path);
            return Results.Json(conflict, RegistryJsonOptions.Default,
                statusCode: StatusCodes.Status409Conflict,
                contentType: "application/problem+json");
        }

        var fieldChanges = RegistryAuditFactory.ComputeFieldChanges(ns, persisted);
        var summary = $"Updated ownership for namespace '{((RegistryNamespace)persisted).DisplayName ?? persisted.Name}'.";
        var audit = RegistryAuditFactory.Build(
            persisted,
            AuditEventType.NamespaceOwnershipUpdated,
            summary,
            principalAccessor,
            timeProvider,
            fieldChanges: fieldChanges);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(persisted.Etag))
        {
            context.Response.Headers.ETag = persisted.Etag;
        }

        var response = mapping.ToResponse((RegistryNamespace)persisted);
        return Results.Json(response, statusCode: StatusCodes.Status200OK);
    }

    private static IResult Problem(int status, string code, string detail, string instance)
        => Results.Problem(
            title: code,
            detail: detail,
            statusCode: status,
            instance: instance,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = code,
            });
}
