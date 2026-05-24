using BusTerminal.Api.Domain;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// Spec 004 / T124 / FR-020 / quickstart Smoke 4. Sets IsDeleted on a resource
// while preserving identifier, audit, version lineage, ownership, lifecycle,
// and relationships per the soft-delete contract. Emits a SoftDeleted event
// into the change-event log.
internal static class SoftDeleteCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("soft-delete: --resource-id is required.");
            return 64;
        }

        var id = ResourceId.Parse(options.ResourceId);
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var registry = services.GetRequiredService<ResourceTypeRegistry>();

        Resource? existing = null;
        foreach (var discriminator in registry.KnownDiscriminators)
        {
            existing = await store.GetAsync(id, discriminator, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                break;
            }
        }

        if (existing is null)
        {
            Console.Error.WriteLine($"soft-delete: no resource found with id {id}.");
            return 1;
        }

        var written = await store.SoftDeleteAsync(
            existing.Id,
            existing.ResourceType,
            existing.ConcurrencyToken,
            ServiceHost.Actor,
            ServiceHost.SourceSystem,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"soft-deleted {written.Id} ({written.ResourceType}); lifecycle preserved at {written.Lifecycle}");
        return 0;
    }
}
