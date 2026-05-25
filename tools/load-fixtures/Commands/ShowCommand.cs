using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T081 — show a single resource by id. Tries every known resourceType partition
// (since the CLI caller usually doesn't know the type).
// T145 (US8) — supports `--format yaml` in addition to `--format json` (default).
internal static class ShowCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("show: --resource-id is required.");
            return 64;
        }

        var format = options.Format?.ToLowerInvariant() ?? "json";
        if (format is not "json" and not "yaml")
        {
            Console.Error.WriteLine($"show: --format must be 'json' or 'yaml' (got '{options.Format}').");
            return 64;
        }

        var id = ResourceId.Parse(options.ResourceId);
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var registry = services.GetRequiredService<ResourceTypeRegistry>();
        var json = services.GetRequiredService<JsonResourceSerializer>();
        var yaml = services.GetRequiredService<YamlResourceSerializer>();

        foreach (var discriminator in registry.KnownDiscriminators)
        {
            var resource = await store.GetAsync(id, discriminator, options.IncludeDeleted, cancellationToken).ConfigureAwait(false);
            if (resource is not null)
            {
                Console.WriteLine(format == "yaml" ? yaml.SerializeToYaml(resource) : json.SerializeToJson(resource));
                return 0;
            }
        }

        Console.Error.WriteLine($"show: no resource found with id {id}.");
        return 1;
    }
}
