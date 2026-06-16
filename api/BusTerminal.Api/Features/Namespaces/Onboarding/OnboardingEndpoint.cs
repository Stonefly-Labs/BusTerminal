using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Onboarding;

// Spec 008 / T080 + FR-023a + contracts/namespace-onboarding-api.yaml.
// POST /api/namespaces — wizard step-5 register.
//
// Pipeline:
//   1. namespace-administrator role gate (via NamespaceAdministratorPolicy).
//   2. FluentValidation of OnboardingRequest — covers ARM-id verification
//      (cross-tenant + already-onboarded) AND ValidationRun freshness /
//      Healthy-or-Degraded (FR-023a hard block).
//   3. Re-read the referenced ValidationRun to confirm `namespaceId ==
//      request.Id` (research §18 binding); mismatch → 400 NamespaceIdMismatch.
//   4. Persist the OnboardedNamespace via IRegistryEntityStore.CreateAsync
//      with `source = Onboarded`, `lifecycleStatus = Active`,
//      `validationStatus` mirroring the run, `lastValidationRunId`,
//      `lastValidatedAtUtc`, ownership block, onboardingActor snapshot.
//   5. Write the NamespaceOnboarded audit event with `actor = current principal`.
//
// Concurrency: first-write wins — duplicate ARM id check is already covered
// by the validator's async rule. Duplicate id collisions surface as the
// store's standard "create with existing id" error.
public static class OnboardingEndpoint
{
    public static IEndpointRouteBuilder MapOnboardingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/namespaces", HandleAsync)
            .RequireAuthorization()
            .RequireNamespaceAdministrator()
            .WithName("NamespaceRegister")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        OnboardingRequest request,
        OnboardingValidator validator,
        IRegistryEntityStore entityStore,
        INamespaceValidationRunStore runStore,
        IAuditEventStore auditStore,
        NamespaceArmIdParser armIdParser,
        IPlatformPrincipalAccessor principalAccessor,
        NamespaceDtoMapping mapping,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "EmptyBody",
                "Request body is required.", context.Request.Path);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            // FR-023a hard-block: surface stale/Unhealthy validation runs as
            // 409 since the wizard's pre-condition contract is violated;
            // everything else maps to 400 ValidationFailed.
            var validationRunError = validation.Errors
                .FirstOrDefault(e => e.PropertyName == nameof(OnboardingRequest.ValidationRunId));
            if (validationRunError is not null)
            {
                return Problem(StatusCodes.Status409Conflict, "ValidationRunStaleOrUnhealthy",
                    validationRunError.ErrorMessage, context.Request.Path);
            }
            var alreadyOnboardedError = validation.Errors
                .FirstOrDefault(e => e.PropertyName == nameof(OnboardingRequest.AzureResourceId)
                    && e.ErrorMessage.Contains("already onboarded", StringComparison.OrdinalIgnoreCase));
            if (alreadyOnboardedError is not null)
            {
                return Problem(StatusCodes.Status409Conflict, "AlreadyOnboarded",
                    alreadyOnboardedError.ErrorMessage, context.Request.Path);
            }
            return Results.ValidationProblem(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                instance: context.Request.Path,
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Re-fetch the validation run for the namespaceId-binding check + to
        // mirror the aggregate status onto the persisted document.
        var run = await runStore
            .GetAsync(request.Id, request.ValidationRunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "ValidationRunNotFound",
                "The referenced validationRunId does not exist for the supplied namespace id.",
                context.Request.Path);
        }
        if (run.NamespaceId != request.Id)
        {
            return Problem(StatusCodes.Status400BadRequest, "NamespaceIdMismatch",
                "validationRunId.namespaceId does not match the request body id.",
                context.Request.Path);
        }

        // ARM parse is cheap given the parser cache — we need the canonical
        // pieces (subscription id, tenant id, region) for the persisted shape.
        var parseResult = await armIdParser
            .ParseAndVerifyAsync(request.AzureResourceId, cancellationToken)
            .ConfigureAwait(false);
        if (!parseResult.IsSuccess || parseResult.ArmId is null || parseResult.TenantId is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "InvalidArmId",
                parseResult.Reason, context.Request.Path);
        }

        var armId = parseResult.ArmId;
        var tenantId = parseResult.TenantId.Value;
        var snapshot = run.ArmResourceSnapshot;
        var region = snapshot?.Region ?? string.Empty;

        var now = timeProvider.GetUtcNow();
        var principal = principalAccessor.Current;
        var actorId = principal?.ObjectId ?? Guid.Empty;
        var actorName = principal?.DisplayName ?? principal?.Username ?? "(unknown)";

        var entity = new RegistryNamespace(
            id: request.Id,
            name: armId.NamespaceName,
            environment: request.Environment,
            status: RegistryEntityStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            source: RegistrySource.Onboarded,
            fullyQualifiedName: armId.NamespaceName,
            description: request.Description,
            tags: request.Tags,
            owner: null,
            azureResourceId: armId.CanonicalArmId,
            metadata: null,
            etag: null)
        {
            DisplayName = request.DisplayName,
            SubscriptionId = armId.SubscriptionId,
            ResourceGroup = armId.ResourceGroup,
            TenantId = tenantId,
            Region = region,
            BusinessUnit = request.BusinessUnit,
            ProductOrApplication = request.ProductOrApplication,
            CostCenter = request.CostCenter,
            Notes = request.Notes,
            LifecycleStatus = LifecycleStatus.Active,
            ValidationStatus = run.AggregateStatus,
            LastValidationRunId = run.Id,
            LastValidatedAtUtc = run.ExecutedAtUtc,
            Ownership = request.Ownership,
            OnboardingActor = new OnboardingActor(
                ObjectId: actorId,
                DisplayNameSnapshot: actorName,
                OnboardedAtUtc: now),
        };

        var created = await entityStore.CreateAsync(entity, cancellationToken).ConfigureAwait(false);

        var changeSummary = $"Onboarded namespace '{request.DisplayName}' in environment '{request.Environment}'.";
        var audit = RegistryAuditFactory.Build(
            created,
            AuditEventType.NamespaceOnboarded,
            changeSummary,
            principalAccessor,
            timeProvider);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        var response = mapping.ToResponse((RegistryNamespace)created);
        if (!string.IsNullOrEmpty(created.Etag))
        {
            context.Response.Headers.ETag = created.Etag;
        }
        context.Response.Headers.Location = $"/api/namespaces/{created.Id:D}";
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
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
