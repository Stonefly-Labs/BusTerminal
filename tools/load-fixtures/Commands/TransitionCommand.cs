using System.Globalization;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// Spec 004 / T124 / quickstart Smoke 3 + Smoke 4. Transition a resource's
// Lifecycle through the legal-transition graph. Reads the existing document so
// the validation engine can compare the prior state against the requested
// target — illegal transitions are rejected by LifecycleTransitionRule
// (Severity: Error) before the write is attempted.
internal static class TransitionCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("transition: --resource-id is required.");
            return 64;
        }

        if (string.IsNullOrWhiteSpace(options.To))
        {
            Console.Error.WriteLine("transition: --to <draft|active|deprecated|retired|archived> is required.");
            return 64;
        }

        if (!TryParseLifecycle(options.To, out var target))
        {
            Console.Error.WriteLine($"transition: --to '{options.To}' is not a known lifecycle state.");
            return 64;
        }

        var id = ResourceId.Parse(options.ResourceId);
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var registry = services.GetRequiredService<ResourceTypeRegistry>();
        var engine = services.GetRequiredService<ValidationEngine>();

        Resource? existing = null;
        foreach (var discriminator in registry.KnownDiscriminators)
        {
            existing = await store.GetAsync(id, discriminator, includeDeleted: false, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                break;
            }
        }

        if (existing is null)
        {
            Console.Error.WriteLine($"transition: no live resource found with id {id}.");
            return 1;
        }

        var updated = existing with { Lifecycle = target };

        var validation = await engine.ValidateAsync(
            updated,
            relationshipResolver: _ => null,
            duplicateDetector: _ => false,
            previousLifecycle: existing.Lifecycle,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (validation.HasErrors)
        {
            foreach (var f in validation.Findings)
            {
                Console.Error.WriteLine($"[rejected] {id} — {f.Severity}: {f.Message}");
            }

            return 1;
        }

        var stamped = updated with { ValidationState = validation };
        var written = await store.UpdateAsync(stamped, ServiceHost.Actor, ServiceHost.SourceSystem, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"transitioned {written.Id} ({written.ResourceType}) {existing.Lifecycle} -> {written.Lifecycle}");
        return 0;
    }

    private static bool TryParseLifecycle(string raw, out LifecycleState state) =>
        Enum.TryParse(raw, ignoreCase: true, out state) &&
        Enum.IsDefined(typeof(LifecycleState), state);
}
