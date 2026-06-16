using System.Text.Json;
using FluentValidation;

namespace BusTerminal.Api.Features.Namespaces.Metadata;

// Spec 008 / data-model.md §5 UpdateMetadataRequest. Length rules + the
// FR-005 Azure-identifier rejection — if any of the immutable Azure fields
// appears in the request body, validation fails with a code that maps to
// 400 on the endpoint.
public sealed class UpdateMetadataValidator : AbstractValidator<UpdateMetadataRequest>
{
    private static readonly string[] ProhibitedKeys =
    [
        "azureResourceId",
        "subscriptionId",
        "subscriptionName",
        "resourceGroup",
        "tenantId",
        "region",
        "namespaceName",
    ];

    public UpdateMetadataValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.BusinessUnit).MaximumLength(200);
        RuleFor(x => x.ProductOrApplication).MaximumLength(200);
        RuleFor(x => x.CostCenter).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);

        RuleFor(x => x.RawBody)
            .Must(NotContainAzureIdentifierFields)
            .WithMessage(
                "Azure identifier fields (azureResourceId, subscriptionId, subscriptionName, resourceGroup, tenantId, region, namespaceName) are immutable post-onboarding and MUST NOT appear in the request body (FR-005).")
            .When(x => x.RawBody.HasValue && x.RawBody.Value.ValueKind == JsonValueKind.Object);
    }

    private static bool NotContainAzureIdentifierFields(JsonElement? body)
    {
        if (!body.HasValue || body.Value.ValueKind != JsonValueKind.Object) return true;
        foreach (var key in ProhibitedKeys)
        {
            if (body.Value.TryGetProperty(key, out _)) return false;
        }
        return true;
    }
}
