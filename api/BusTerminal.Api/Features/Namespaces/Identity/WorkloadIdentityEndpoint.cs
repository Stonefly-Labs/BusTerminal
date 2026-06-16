using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusTerminal.Api.Features.Namespaces.Identity;

// Spec 008 / T077 + FR-014 / research §17. GET /api/namespaces/identity —
// surfaces the workload UAMI's principalId so the wizard step-1 sidebar can
// build a copy-pasteable `az role assignment create` command. AuthN-only
// (no namespace-administrator role required) so any authenticated user can
// preview the prerequisite before requesting administrator access.
//
// The runbook URL points at iac/runbooks/grant-namespace-reader.md surfaced
// via the deployed docs path (configurable via NamespaceOnboarding:RunbookUrl
// — defaults to the GitHub repo location).
public static partial class WorkloadIdentityEndpoint
{
    private const string DefaultRunbookUrl =
        "https://github.com/chris-house/BusTerminal/blob/main/iac/runbooks/grant-namespace-reader.md";

    [LoggerMessage(
        EventId = 8601,
        Level = LogLevel.Error,
        Message = "Workload identity unavailable; deployment misconfiguration suspected.")]
    private static partial void LogWorkloadIdentityUnavailable(ILogger logger, Exception exception);

    public static IEndpointRouteBuilder MapWorkloadIdentityEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/namespaces/identity", HandleAsync)
            .RequireAuthorization()
            .WithName("NamespaceWorkloadIdentity")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        WorkloadIdentityProvider provider,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var principalId = await provider.GetPrincipalIdAsync(cancellationToken).ConfigureAwait(false);
            var clientId = ParseClientId(configuration["AZURE_CLIENT_ID"]);
            var runbookUrl = configuration["NamespaceOnboarding:RunbookUrl"] ?? DefaultRunbookUrl;
            var sampleCommand =
                $"az role assignment create --assignee {principalId:D} --role Reader --scope {{azureResourceId}}";

            return Results.Ok(new WorkloadIdentityResponse(
                PrincipalId: principalId,
                ClientId: clientId,
                RunbookUrl: runbookUrl,
                SampleGrantCommand: sampleCommand));
        }
        catch (InvalidOperationException ex)
        {
            // WorkloadIdentityProvider raises InvalidOperationException when
            // the WORKLOAD_PRINCIPAL_ID env var is missing or unparseable —
            // a deployment misconfiguration. Surface as 500 with a hint
            // pointing at the runbook.
            LogWorkloadIdentityUnavailable(
                loggerFactory.CreateLogger("BusTerminal.Namespaces.Identity"),
                ex);
            return Results.Problem(
                title: "Workload identity unavailable",
                detail: "WORKLOAD_PRINCIPAL_ID is not configured. See deployment runbook.",
                statusCode: StatusCodes.Status500InternalServerError,
                instance: context.Request.Path);
        }
    }

    private static Guid? ParseClientId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Guid.TryParse(raw, out var parsed) ? parsed : (Guid?)null;
    }
}

// Spec 008 / contracts/namespace-onboarding-api.yaml#/WorkloadIdentity.
public sealed record WorkloadIdentityResponse(
    Guid PrincipalId,
    Guid? ClientId,
    string RunbookUrl,
    string SampleGrantCommand);
