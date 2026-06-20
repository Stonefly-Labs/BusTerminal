using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using FluentValidation;

namespace BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;

// Spec 009 / T103. Body validation for PATCH /api/entities/{id}. Enforces:
//   • Documented limits per data-model.md §1.1 + tasks.md T103.
//   • Curated-only keys (azureSourced.* is rejected at the parsing layer
//     before this validator runs because the request shape doesn't include
//     azureSourced; ExtraDataReject() catches anything that slipped in).
//   • URL format for documentationLinks.
//   • Tag count ≤ 32, tag length ≤ 64 chars.
//   • String length caps that match the AI Search field sizes.
public sealed class UpdateEntityMetadataValidator : AbstractValidator<UpdateEntityMetadataRequest>
{
    public const int MaxDescriptionLength = 4_000;
    public const int MaxBusinessPurposeLength = 2_000;
    public const int MaxOperationalNotesLength = 8_000;
    public const int MaxContactStringLength = 512;
    public const int MaxTagLength = 64;
    public const int MaxTagCount = 32;
    public const int MaxDocumentationLinkCount = 32;
    public const int MaxDocumentationLinkLabelLength = 128;

    public UpdateEntityMetadataValidator()
    {
        RuleFor(r => r).Custom((req, ctx) =>
        {
            ValidateStringField(req.Description, "description", MaxDescriptionLength, ctx);
            ValidateStringField(req.BusinessPurpose, "businessPurpose", MaxBusinessPurposeLength, ctx);
            ValidateStringField(req.OperationalNotes, "operationalNotes", MaxOperationalNotesLength, ctx);
            ValidateTags(req.Tags, ctx);
            ValidateDocumentationLinks(req.DocumentationLinks, ctx);
            ValidateContactInformation(req.ContactInformation, ctx);
        });
    }

    private static void ValidateStringField(JsonElement element, string field, int maxLength, ValidationContext<UpdateEntityMetadataRequest> ctx)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return;
        if (element.ValueKind == JsonValueKind.Null) return;
        if (element.ValueKind != JsonValueKind.String)
        {
            ctx.AddFailure(field, $"{field} must be a string or null.");
            return;
        }
        var value = element.GetString();
        if (value is not null && value.Length > maxLength)
        {
            ctx.AddFailure(field, $"{field} must be {maxLength} characters or fewer.");
        }
    }

    private static void ValidateTags(JsonElement element, ValidationContext<UpdateEntityMetadataRequest> ctx)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return;
        if (element.ValueKind == JsonValueKind.Null) return;
        if (element.ValueKind != JsonValueKind.Array)
        {
            ctx.AddFailure("tags", "tags must be an array of strings.");
            return;
        }
        if (element.GetArrayLength() > MaxTagCount)
        {
            ctx.AddFailure("tags", $"tags must contain {MaxTagCount} entries or fewer.");
            return;
        }
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                ctx.AddFailure($"tags[{index}]", "Each tag must be a string.");
            }
            else
            {
                var v = item.GetString();
                if (string.IsNullOrWhiteSpace(v))
                {
                    ctx.AddFailure($"tags[{index}]", "Tags must be non-empty.");
                }
                else if (v.Length > MaxTagLength)
                {
                    ctx.AddFailure($"tags[{index}]", $"Each tag must be {MaxTagLength} characters or fewer.");
                }
            }
            index++;
        }
    }

    private static void ValidateDocumentationLinks(JsonElement element, ValidationContext<UpdateEntityMetadataRequest> ctx)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return;
        if (element.ValueKind == JsonValueKind.Null) return;
        if (element.ValueKind != JsonValueKind.Array)
        {
            ctx.AddFailure("documentationLinks", "documentationLinks must be an array.");
            return;
        }
        if (element.GetArrayLength() > MaxDocumentationLinkCount)
        {
            ctx.AddFailure("documentationLinks", $"documentationLinks must contain {MaxDocumentationLinkCount} entries or fewer.");
            return;
        }
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                ctx.AddFailure($"documentationLinks[{index}]", "Each link must be an object with label + url.");
                index++;
                continue;
            }
            if (!item.TryGetProperty("label", out var labelProp) || labelProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(labelProp.GetString()))
            {
                ctx.AddFailure($"documentationLinks[{index}].label", "label is required and must be a non-empty string.");
            }
            else if (labelProp.GetString()!.Length > MaxDocumentationLinkLabelLength)
            {
                ctx.AddFailure($"documentationLinks[{index}].label", $"label must be {MaxDocumentationLinkLabelLength} characters or fewer.");
            }
            if (!item.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(urlProp.GetString()))
            {
                ctx.AddFailure($"documentationLinks[{index}].url", "url is required and must be a non-empty string.");
            }
            else if (!Uri.TryCreate(urlProp.GetString(), UriKind.Absolute, out var parsed)
                     || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                ctx.AddFailure($"documentationLinks[{index}].url", "url must be an absolute http or https URL.");
            }
            index++;
        }
    }

    private static void ValidateContactInformation(JsonElement element, ValidationContext<UpdateEntityMetadataRequest> ctx)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return;
        if (element.ValueKind == JsonValueKind.Null) return;
        if (element.ValueKind != JsonValueKind.Object)
        {
            ctx.AddFailure("contactInformation", "contactInformation must be an object or null.");
            return;
        }
        if (element.TryGetProperty("primaryContact", out var primary)
            && primary.ValueKind == JsonValueKind.String
            && primary.GetString()!.Length > MaxContactStringLength)
        {
            ctx.AddFailure("contactInformation.primaryContact", $"primaryContact must be {MaxContactStringLength} characters or fewer.");
        }
        if (element.TryGetProperty("escalationPath", out var esc)
            && esc.ValueKind == JsonValueKind.String
            && esc.GetString()!.Length > MaxContactStringLength)
        {
            ctx.AddFailure("contactInformation.escalationPath", $"escalationPath must be {MaxContactStringLength} characters or fewer.");
        }
    }
}

// Spec 009 / T103. Converts the JsonElement-shaped request into the typed
// CuratedMetadataPatch the store consumes. The endpoint runs validator
// first, then this mapper — so by the time we reach it, we know each field
// is well-formed.
public static class UpdateEntityMetadataMapper
{
    public static CuratedMetadataPatch ToPatch(UpdateEntityMetadataRequest request)
    {
        return new CuratedMetadataPatch(
            Description: ReadOptionalString(request.Description),
            BusinessPurpose: ReadOptionalString(request.BusinessPurpose),
            Tags: ReadOptionalStringList(request.Tags),
            DocumentationLinks: ReadOptionalLinks(request.DocumentationLinks),
            ContactInformation: ReadOptionalContact(request.ContactInformation),
            OperationalNotes: ReadOptionalString(request.OperationalNotes));
    }

    private static OptionalValue<string?> ReadOptionalString(JsonElement el) =>
        el.ValueKind == JsonValueKind.Undefined
            ? OptionalValue<string?>.Unset()
            : OptionalValue<string?>.Set(el.ValueKind == JsonValueKind.Null ? null : el.GetString());

    private static OptionalValue<IReadOnlyList<string>?> ReadOptionalStringList(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return OptionalValue<IReadOnlyList<string>?>.Unset();
        if (el.ValueKind == JsonValueKind.Null) return OptionalValue<IReadOnlyList<string>?>.Set(null);
        var list = new List<string>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            list.Add(item.GetString() ?? string.Empty);
        }
        return OptionalValue<IReadOnlyList<string>?>.Set(list);
    }

    private static OptionalValue<IReadOnlyList<EntityDocumentationLink>?> ReadOptionalLinks(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return OptionalValue<IReadOnlyList<EntityDocumentationLink>?>.Unset();
        if (el.ValueKind == JsonValueKind.Null) return OptionalValue<IReadOnlyList<EntityDocumentationLink>?>.Set(null);
        var list = new List<EntityDocumentationLink>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            var label = item.GetProperty("label").GetString() ?? string.Empty;
            var url = item.GetProperty("url").GetString() ?? string.Empty;
            list.Add(new EntityDocumentationLink(label, url));
        }
        return OptionalValue<IReadOnlyList<EntityDocumentationLink>?>.Set(list);
    }

    private static OptionalValue<EntityContactInformation?> ReadOptionalContact(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return OptionalValue<EntityContactInformation?>.Unset();
        if (el.ValueKind == JsonValueKind.Null) return OptionalValue<EntityContactInformation?>.Set(null);
        string? primary = el.TryGetProperty("primaryContact", out var pc) && pc.ValueKind == JsonValueKind.String ? pc.GetString() : null;
        string? esc = el.TryGetProperty("escalationPath", out var ep) && ep.ValueKind == JsonValueKind.String ? ep.GetString() : null;
        return OptionalValue<EntityContactInformation?>.Set(new EntityContactInformation(primary, esc));
    }
}
