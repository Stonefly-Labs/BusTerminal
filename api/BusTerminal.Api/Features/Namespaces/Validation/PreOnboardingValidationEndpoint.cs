using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Validation;

// Spec 008 / T079 + research §18 + contracts/namespace-onboarding-api.yaml#/_validate.
// POST /api/namespaces/_validate — wizard step-4 pre-onboarding validation
// surface. Accepts an optional `proposedNamespaceId` so the wizard's
// pre-allocated Guid binds the ValidationRun.namespaceId for partition
// alignment with the eventual OnboardedNamespace document. Direct API
// callers MAY omit it; the runner generates a fresh Guid in that case.
//
// AuthN-only — any authenticated tenant user may run the pre-onboarding
// probe (matches the surface-level reasoning for the identity + picker
// endpoints; the namespace-administrator role is only required at the
// step-5 register call, T080).
public static class PreOnboardingValidationEndpoint
{
    public static IEndpointRouteBuilder MapPreOnboardingValidationEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/namespaces/_validate", HandleAsync)
            .RequireAuthorization()
            .WithName("NamespacePreOnboardingValidate")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        PreOnboardingValidationRequest request,
        NamespaceArmIdParser parser,
        NamespaceValidationRunner runner,
        IPlatformPrincipalAccessor principalAccessor,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.AzureResourceId))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "InvalidRequest",
                "azureResourceId is required.",
                context.Request.Path);
        }

        var parseResult = await parser
            .ParseAndVerifyAsync(request.AzureResourceId, cancellationToken)
            .ConfigureAwait(false);

        if (!parseResult.IsSuccess)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                parseResult.FailureCategory == ValidationFailureCategory.CrossTenant
                    ? "CrossTenantArmId"
                    : "InvalidArmId",
                parseResult.Reason,
                context.Request.Path);
        }

        var principal = principalAccessor.Current;
        var executedBy = principal?.ObjectId ?? Guid.Empty;
        var displayName = principal?.DisplayName
            ?? principal?.Username
            ?? "(unknown)";

        var runRequest = new NamespaceValidationRunRequest(
            RunId: Guid.NewGuid(),
            NamespaceId: request.ProposedNamespaceId ?? Guid.NewGuid(),
            ArmId: parseResult.ArmId!,
            Environment: null,
            ExecutedBy: executedBy,
            ExecutedByDisplayNameSnapshot: displayName,
            PersistedDriftBaseline: null);

        var run = await runner
            .ExecuteAsync(runRequest, NamespaceValidationActivitySource.OnboardingRunSpan, cancellationToken)
            .ConfigureAwait(false);

        return Results.Json(run, statusCode: StatusCodes.Status201Created);
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

// Spec 008 / contracts/namespace-onboarding-api.yaml#/PreOnboardingValidationRequest.
public sealed record PreOnboardingValidationRequest(
    string AzureResourceId,
    Guid? ProposedNamespaceId);
