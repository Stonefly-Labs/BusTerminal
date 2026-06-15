using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Namespaces.Validation;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Namespaces.Lifecycle;

// Spec 008 / T130 + FR-023 + FR-024 + contracts/namespace-onboarding-api.yaml#/lifecycle.
// POST /api/namespaces/{id}/lifecycle.
//
// Pipeline:
//   1. namespace-administrator role gate.
//   2. If-Match required.
//   3. 404 when missing or not source=Onboarded.
//   4. LifecycleTransitionValidator (validates action + reason + permitted transition).
//   5. Apply target LifecycleStatus per the action.
//   6. If action == Enable (Disabled → Active): automatically run validation
//      (FR-024) and update lastValidationRunId / lastValidatedAtUtc / validationStatus.
//   7. Persist via UpdateAsync with ETag concurrency.
//   8. Write NamespaceLifecycleTransitioned audit event carrying LifecycleReason.
//   9. Return 200 + new ETag.
public static class TransitionLifecycleEndpoint
{
    public static IEndpointRouteBuilder MapTransitionLifecycleEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/namespaces/{id:guid}/lifecycle", HandleAsync)
            .RequireAuthorization()
            .RequireNamespaceAdministrator()
            .WithName("NamespaceLifecycleTransition")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        LifecycleTransitionRequest? request,
        IRegistryEntityStore entityStore,
        IAuditEventStore auditStore,
        LifecycleTransitionValidator validator,
        ConcurrencyConflictMapper conflictMapper,
        NamespaceArmIdParser armIdParser,
        NamespaceValidationRunner runner,
        NamespaceDtoMapping mapping,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchValues)
            || string.IsNullOrEmpty(ifMatchValues.ToString()))
        {
            return Problem(StatusCodes.Status428PreconditionRequired, "IfMatchRequired",
                "POST /lifecycle requires the If-Match header carrying the entity's current ETag.",
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
        if (ns.LifecycleStatus is not LifecycleStatus currentStatus)
        {
            return Problem(StatusCodes.Status409Conflict, "MissingLifecycleStatus",
                "Persisted namespace lacks a lifecycleStatus field — cannot compute transition.",
                context.Request.Path);
        }

        var validation = await validator
            .ValidateAsync(new LifecycleTransitionValidationInput(request, currentStatus), cancellationToken)
            .ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                instance: context.Request.Path,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var targetStatus = request.Action switch
        {
            LifecycleAction.Disable => LifecycleStatus.Disabled,
            LifecycleAction.Enable => LifecycleStatus.Active,
            LifecycleAction.Archive => LifecycleStatus.Archived,
            LifecycleAction.Restore => LifecycleStatus.Disabled,
            _ => currentStatus,
        };

        var now = timeProvider.GetUtcNow();
        var updatedEntity = ns with { UpdatedAtUtc = now } with { LifecycleStatus = targetStatus };

        // FR-024: re-enabling a disabled namespace automatically triggers a
        // fresh validation run so the namespace is verified before being
        // marked Active again.
        ValidationRun? autoRun = null;
        if (request.Action == LifecycleAction.Enable && !string.IsNullOrEmpty(ns.AzureResourceId))
        {
            var parse = await armIdParser
                .ParseAndVerifyAsync(ns.AzureResourceId, cancellationToken)
                .ConfigureAwait(false);
            if (parse.IsSuccess && parse.ArmId is not null)
            {
                var principal = principalAccessor.Current;
                var runRequest = new NamespaceValidationRunRequest(
                    RunId: Guid.NewGuid(),
                    NamespaceId: ns.Id,
                    ArmId: parse.ArmId,
                    Environment: ns.Environment,
                    ExecutedBy: principal?.ObjectId ?? Guid.Empty,
                    ExecutedByDisplayNameSnapshot: principal?.DisplayName ?? principal?.Username ?? "(unknown)",
                    PersistedDriftBaseline: BuildBaseline(ns));
                autoRun = await runner
                    .ExecuteAsync(runRequest, NamespaceValidationActivitySource.ValidationRerunSpan, cancellationToken)
                    .ConfigureAwait(false);
                updatedEntity = updatedEntity with
                {
                    LastValidationRunId = autoRun.Id,
                    LastValidatedAtUtc = autoRun.ExecutedAtUtc,
                    ValidationStatus = autoRun.AggregateStatus,
                };
            }
        }

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

        var summary = $"Transitioned namespace '{((RegistryNamespace)persisted).DisplayName ?? persisted.Name}' from {currentStatus} via {request.Action} to {targetStatus}.";
        var fieldChanges = RegistryAuditFactory.ComputeFieldChanges(ns, persisted);
        var audit = RegistryAuditFactory.Build(
            persisted,
            AuditEventType.NamespaceLifecycleTransitioned,
            summary,
            principalAccessor,
            timeProvider,
            fieldChanges: fieldChanges);
        audit = audit with { LifecycleReason = request.Reason };
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(persisted.Etag))
        {
            context.Response.Headers.ETag = persisted.Etag;
        }

        var response = mapping.ToResponse((RegistryNamespace)persisted);
        return Results.Json(response, statusCode: StatusCodes.Status200OK);
    }

    private static PersistedNamespaceBaseline? BuildBaseline(RegistryNamespace ns)
    {
        if (string.IsNullOrEmpty(ns.Region) || string.IsNullOrEmpty(ns.ResourceGroup) || !ns.SubscriptionId.HasValue)
        {
            return null;
        }
        return new PersistedNamespaceBaseline(ns.Region!, ns.ResourceGroup!, ns.SubscriptionId.Value);
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
