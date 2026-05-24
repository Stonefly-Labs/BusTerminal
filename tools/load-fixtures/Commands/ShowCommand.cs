using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T081 — show a single resource by id. Tries every known resourceType partition
// (since the CLI caller usually doesn't know the type). YAML output is deferred
// to US8 (T145).
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

        if (!string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"show: --format '{options.Format}' is not supported in Phase 3 (YAML lands in US8 / T145).");
            return 64;
        }

        var id = ResourceId.Parse(options.ResourceId);
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var registry = services.GetRequiredService<ResourceTypeRegistry>();
        var serializer = services.GetRequiredService<JsonResourceSerializer>();

        foreach (var discriminator in registry.KnownDiscriminators)
        {
            var resource = await store.GetAsync(id, discriminator, options.IncludeDeleted, cancellationToken).ConfigureAwait(false);
            if (resource is not null)
            {
                Console.WriteLine(serializer.SerializeToJson(resource));
                return 0;
            }
        }

        Console.Error.WriteLine($"show: no resource found with id {id}.");
        return 1;
    }
}
