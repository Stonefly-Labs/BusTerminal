using System.Text.Json;
using System.Text.Json.Nodes;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// Spec 004 / T125 / FR-015 / quickstart Smoke 8. Print the ordered change-event
// log for a single resource. Backed by IChangeEventLog.QueryAsync, which is
// already ordered by timestamp ascending — the CLI is a thin formatter.
internal static class ChangelogCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("changelog: --resource-id is required.");
            return 64;
        }

        var id = ResourceId.Parse(options.ResourceId);
        var log = services.GetRequiredService<IChangeEventLog>();

        var events = new JsonArray();
        await foreach (var evt in log.QueryAsync(id, cancellationToken).ConfigureAwait(false))
        {
            events.Add(new JsonObject
            {
                ["id"] = evt.Id.ToString(),
                ["resourceId"] = evt.ResourceId.ToString(),
                ["resourceType"] = evt.ResourceType,
                ["eventType"] = evt.EventType.ToString(),
                ["timestamp"] = evt.Timestamp.ToString("O"),
                ["actor"] = evt.Actor switch
                {
                    HumanPrincipalReference h => $"human:{h.ObjectId}",
                    WorkloadPrincipalReference w => $"workload:{w.ObjectId}",
                    SystemPrincipalReference s => $"system:{s.SystemName}",
                    _ => evt.Actor.ToString(),
                },
                ["sourceSystem"] = evt.SourceSystem,
                ["lifecycleBefore"] = evt.LifecycleBefore?.ToString(),
                ["lifecycleAfter"] = evt.LifecycleAfter?.ToString(),
                ["concurrencyTokenBefore"] = evt.ConcurrencyTokenBefore?.Value,
                ["concurrencyTokenAfter"] = evt.ConcurrencyTokenAfter.Value,
            });
        }

        var output = new JsonObject
        {
            ["resourceId"] = id.ToString(),
            ["eventCount"] = events.Count,
            ["events"] = events,
        };

        Console.WriteLine(output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
