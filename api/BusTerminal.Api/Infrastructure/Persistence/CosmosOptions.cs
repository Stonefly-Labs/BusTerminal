using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-018, FR-025. Bound from configuration section "Cosmos".
// Endpoint precedence (environment variables override appsettings) follows the
// standard ASP.NET configuration layering — no special handling needed here.
public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string Database { get; set; } = "busterminal-canonical";

    [Required]
    public ContainerNames Containers { get; set; } = new();

    // Well-known emulator key — intentional public test data per research §2.
    // Used only when Endpoint host is `localhost` to satisfy the emulator's
    // shared-key path. Production / dev Azure auth uses Managed Identity via
    // DefaultAzureCredential.
    public string? LocalEmulatorKey { get; set; } =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    public sealed class ContainerNames
    {
        [Required]
        public string Resources { get; set; } = "resources";

        [Required]
        public string ChangeEvents { get; set; } = "change-events";
    }
}

internal sealed class CosmosOptionsValidator : IValidateOptions<CosmosOptions>
{
    public ValidateOptionsResult Validate(string? name, CosmosOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            errors.Add("Cosmos:Endpoint must be set (e.g., https://<account>.documents.azure.com:443/).");
        }
        else if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            errors.Add($"Cosmos:Endpoint is not a valid absolute URI: '{options.Endpoint}'.");
        }

        if (string.IsNullOrWhiteSpace(options.Database))
        {
            errors.Add("Cosmos:Database must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Containers.Resources))
        {
            errors.Add("Cosmos:Containers:Resources must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Containers.ChangeEvents))
        {
            errors.Add("Cosmos:Containers:ChangeEvents must be set.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
