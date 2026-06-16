using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Validation;

// Spec 008 / T131 + contracts/namespace-onboarding-api.yaml#/validation-runs (POST).
// POST /api/namespaces/{id}/validation-runs.
//
// Pipeline:
//   1. namespace-administrator role gate.
//   2. 404 when missing or not source=Onboarded.
//   3. 409 when LifecycleStatus == Archived (Archived namespaces are read-only).
//   4. Re-parse ARM id (drift-baseline computation also fed from persisted doc).
//   5. NamespaceValidationRunner under the `namespace.validation.rerun` span.
//   6. Best-effort optimistic update of lastValidationRunId / lastValidatedAtUtc
//      / validationStatus via ETag. We tolerate concurrent edits gracefully by
//      logging on conflict and returning the persisted run regardless — the
//      ValidationRun itself is the durable artifact (FR-016).
//   7. Write NamespaceValidationExecuted audit event.
//   8. Return 201 + ValidationRun body.
public static class RunValidationEndpoint
{
    public static IEndpointRouteBuilder MapRunValidationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/namespaces/{id:guid}/validation-runs", HandleAsync)
            .RequireAuthorization()
            .RequireNamespaceAdministrator()
            .WithName("NamespaceValidationRunCreate")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        IRegistryEntityStore entityStore,
        IAuditEventStore auditStore,
        NamespaceArmIdParser armIdParser,
        NamespaceValidationRunner runner,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var current = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not RegistryNamespace ns || ns.Source != RegistrySource.Onboarded)
        {
            return Problem(StatusCodes.Status404NotFound, "NotFound",
                $"No onboarded namespace with id {id:D} was found.", context.Request.Path);
        }

        if (ns.LifecycleStatus == LifecycleStatus.Archived)
        {
            return Problem(StatusCodes.Status409Conflict, "ArchivedNamespaceReadOnly",
                "Archived namespaces are read-only. Restore before running validation.",
                context.Request.Path);
        }

        if (string.IsNullOrEmpty(ns.AzureResourceId))
        {
            return Problem(StatusCodes.Status409Conflict, "MissingAzureResourceId",
                "Persisted namespace document has no azureResourceId — cannot run validation.",
                context.Request.Path);
        }

        var parse = await armIdParser
            .ParseAndVerifyAsync(ns.AzureResourceId, cancellationToken)
            .ConfigureAwait(false);
        if (!parse.IsSuccess || parse.ArmId is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "InvalidArmId",
                parse.Reason, context.Request.Path);
        }

        var principal = principalAccessor.Current;
        var runRequest = new NamespaceValidationRunRequest(
            RunId: Guid.NewGuid(),
            NamespaceId: ns.Id,
            ArmId: parse.ArmId,
            Environment: ns.Environment,
            ExecutedBy: principal?.ObjectId ?? Guid.Empty,
            ExecutedByDisplayNameSnapshot: principal?.DisplayName ?? principal?.Username ?? "(unknown)",
            PersistedDriftBaseline: BuildBaseline(ns));

        var run = await runner
            .ExecuteAsync(runRequest, NamespaceValidationActivitySource.ValidationRerunSpan, cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var updated = ns with { UpdatedAtUtc = now } with
        {
            LastValidationRunId = run.Id,
            LastValidatedAtUtc = run.ExecutedAtUtc,
            ValidationStatus = run.AggregateStatus,
        };

        RegistryEntity persisted = ns;
        if (!string.IsNullOrEmpty(ns.Etag))
        {
            try
            {
                persisted = await entityStore.UpdateAsync(updated, ns.Etag, cancellationToken).ConfigureAwait(false);
            }
            catch (RegistryConcurrencyConflictException)
            {
                // Tolerate concurrent edits — the ValidationRun is persisted
                // independently and remains queryable via list/get; a future
                // re-run will reconcile the namespace document.
                persisted = updated;
            }
        }
        else
        {
            persisted = updated;
        }

        var summary = $"Executed validation for namespace '{((RegistryNamespace)persisted).DisplayName ?? persisted.Name}' — aggregate {run.AggregateStatus}.";
        var audit = RegistryAuditFactory.Build(
            persisted,
            AuditEventType.NamespaceValidationExecuted,
            summary,
            principalAccessor,
            timeProvider);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        return Results.Json(run, statusCode: StatusCodes.Status201Created);
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
