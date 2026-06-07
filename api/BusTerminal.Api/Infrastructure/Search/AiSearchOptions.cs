using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Search;

// Spec 006 / T037 / data-model.md §6. Configuration for the Azure AI Search
// client. Endpoint + index name are bound from configuration; the SDK uses
// DefaultAzureCredential against the workload UAMI (research §7).
public sealed class AiSearchOptions
{
    public const string SectionName = "AiSearch";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string IndexName { get; set; } = "registry-entities-v1";
}

internal sealed class AiSearchOptionsValidator : IValidateOptions<AiSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, AiSearchOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            errors.Add("AiSearch:Endpoint must be set (e.g., https://<service>.search.windows.net).");
        }
        else if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            errors.Add($"AiSearch:Endpoint is not a valid absolute URI: '{options.Endpoint}'.");
        }
        if (string.IsNullOrWhiteSpace(options.IndexName))
        {
            errors.Add("AiSearch:IndexName must be set.");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}
