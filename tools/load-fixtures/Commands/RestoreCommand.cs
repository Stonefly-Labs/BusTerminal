using BusTerminal.Api.Domain;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// Spec 004 / T124 / FR-020. Reverse of SoftDeleteCommand: clears IsDeleted and
// restores the resource to its preserved Lifecycle. The store bypasses
// LifecycleTransitionRule for restore because soft-delete + restoration are
// orthogonal to lifecycle transitions per
// contracts/lifecycle-transitions.md. Emits a Restored event.
internal static class RestoreCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("restore: --resource-id is required.");
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
            Console.Error.WriteLine($"restore: no resource found with id {id}.");
            return 1;
        }

        var written = await store.RestoreAsync(
            existing.Id,
            existing.ResourceType,
            existing.ConcurrencyToken,
            ServiceHost.Actor,
            ServiceHost.SourceSystem,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"restored {written.Id} ({written.ResourceType}); lifecycle = {written.Lifecycle}");
        return 0;
    }
}
