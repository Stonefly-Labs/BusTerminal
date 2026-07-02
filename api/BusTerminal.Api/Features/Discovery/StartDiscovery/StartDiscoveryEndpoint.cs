using System.Diagnostics;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using BusTerminal.Api.Features.Namespaces.Shared;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Discovery.StartDiscovery;

// Spec 009 / T045 + FR-003 / FR-027.
// POST /api/namespaces/{namespaceId}/discover
//
// Pipeline:
//   1. Authenticated + namespace-administrator role gate.
//   2. Optional body validation (empty for v1).
//   3. Namespace existence + lifecycle gate.
//   4. Coalesce-or-start a discovery run.
//   5. If newly started, publish the envelope to the internal Service Bus queue.
//   6. Return 202 + StartDiscoveryResponse with `coalescedFromExisting`.
//
// Telemetry: emits the `discovery.api.start` span with the runId + namespace
// id; the coalescing decision is recorded as a span tag.
public static class StartDiscoveryEndpoint
{
    public static IEndpointRouteBuilder MapStartDiscoveryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/namespaces/{namespaceId:guid}/discover", HandleAsync)
            .RequireAuthorization()
            .RequireNamespaceAdministrator()
            .WithName("StartDiscovery")
            .WithTags("Discovery");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid namespaceId,
        IValidator<StartDiscoveryRequest> validator,
        IStartDiscoveryNamespaceGate namespaceGate,
        IDiscoveryRunCoalescer coalescer,
        IDiscoveryRequestPublisher publisher,
        IPlatformPrincipalAccessor principalAccessor,
        CancellationToken cancellationToken)
    {
        var request = new StartDiscoveryRequest();
        var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(
                validationResult.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                instance: context.Request.Path,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var gate = await namespaceGate.CheckAsync(namespaceId, cancellationToken).ConfigureAwait(false);
        switch (gate.Outcome)
        {
            case NamespaceDiscoveryGate.NamespaceNotFound:
                return Problem(StatusCodes.Status404NotFound, "NamespaceNotFound",
                    gate.Reason ?? "Namespace not found.", context.Request.Path);
            case NamespaceDiscoveryGate.LifecycleBlocked:
                return Problem(StatusCodes.Status409Conflict, "LifecycleBlocked",
                    gate.Reason ?? "Namespace cannot be discovered in its current lifecycle state.",
                    context.Request.Path);
        }

        var principal = principalAccessor.Current;
        var requestedBy = principal?.ObjectId.ToString("D") ?? "00000000-0000-0000-0000-000000000000";
        // Correlation ID — prefer the inbound W3C traceparent so the API span
        // and the worker span share a trace.
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        using var span = DiscoveryActivitySource.Instance.StartActivity(
            DiscoveryActivitySource.SpanNames.ApiStartDiscovery, ActivityKind.Server);
        span?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, namespaceId.ToString("D"));

        var coalesce = await coalescer.EnsureRunAsync(
            namespaceId.ToString("D"), requestedBy, correlationId, cancellationToken).ConfigureAwait(false);

        span?.SetTag(DiscoveryActivitySource.AttributeKeys.RunId, coalesce.DiscoveryRunId);
        span?.SetTag(DiscoveryActivitySource.AttributeKeys.CoalescedFromExisting, coalesce.CoalescedFromExisting);

        if (!coalesce.CoalescedFromExisting)
        {
            var envelope = new DiscoveryRequestEnvelope(
                DiscoveryRunId: coalesce.DiscoveryRunId,
                NamespaceId: namespaceId.ToString("D"),
                RequestedBy: requestedBy,
                RequestedUtc: coalesce.StartedUtc,
                CorrelationId: correlationId);
            try
            {
                await publisher.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Issue #116 — the run + lock are already durable; if the
                // message never made it onto the queue, no worker will ever
                // process the run and every later request would coalesce onto
                // it. Compensate (mark Failed + release lock) so the next
                // request re-publishes, and surface the failure instead of a
                // phantom 202/Queued. Compensation uses CancellationToken.None
                // so a client disconnect can't leave the wedge in place.
                await coalescer.AbandonQueuedRunAsync(
                    namespaceId.ToString("D"), coalesce.DiscoveryRunId,
                    $"Failed to enqueue discovery request: {ex.Message}",
                    CancellationToken.None).ConfigureAwait(false);
                span?.SetStatus(ActivityStatusCode.Error, "discovery-requested publish failed");
                if (ex is OperationCanceledException)
                {
                    throw;
                }
                return Problem(StatusCodes.Status502BadGateway, "DiscoveryEnqueueFailed",
                    "The discovery request could not be enqueued. No run was started; retry the request.",
                    context.Request.Path);
            }
        }

        var response = new StartDiscoveryResponse(
            DiscoveryRunId: coalesce.DiscoveryRunId,
            NamespaceId: namespaceId.ToString("D"),
            Status: coalesce.Status,
            CoalescedFromExisting: coalesce.CoalescedFromExisting,
            StartedUtc: coalesce.StartedUtc);

        return Results.Json(response, statusCode: StatusCodes.Status202Accepted);
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
