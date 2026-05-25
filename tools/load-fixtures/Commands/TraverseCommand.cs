using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T109 / FR-008 / quickstart Smoke 2. Traverses the relationship graph from a
// starting resource and prints the typed multi-hop path as JSON.
//
// Output schema:
//   {
//     "startId": "<guid>",
//     "direction": "outbound|inbound|both",
//     "maxHops": N,
//     "typesFilter": ["publishesTo", ...] | null,
//     "hops": [ { "depth": 1, "from": "<id>", "to": "<id>", "type": "publishesTo", "relationshipId": "<id>" }, ... ],
//     "visitedCount": N
//   }
internal static class TraverseCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("traverse: --from <resource-id> is required.");
            return 64;
        }

        var startId = ResourceId.Parse(options.ResourceId);
        var maxHops = options.MaxHops ?? 5;
        if (maxHops < 0)
        {
            Console.Error.WriteLine("traverse: --max-hops must be non-negative.");
            return 64;
        }

        var direction = ParseDirection(options.To);
        var allowedTypes = ParseTypes(options.Types);

        var graph = services.GetRequiredService<RelationshipGraph>();
        var result = await graph
            .TraverseAsync(startId, allowedTypes, maxHops, direction, cancellationToken)
            .ConfigureAwait(false);

        var hopsArray = new JsonArray();
        foreach (var hop in result.Hops)
        {
            hopsArray.Add(new JsonObject
            {
                ["depth"] = hop.Depth,
                ["from"] = hop.From.ToString(),
                ["to"] = hop.To.ToString(),
                ["type"] = RelationshipTypeWireForCli.Of(hop.Type),
                ["relationshipId"] = hop.RelationshipId.ToString(),
            });
        }

        var typesNode = allowedTypes is null
            ? (JsonNode?)null
            : new JsonArray([.. allowedTypes.Select(t => (JsonNode?)RelationshipTypeWireForCli.Of(t))]);

        var output = new JsonObject
        {
            ["startId"] = startId.ToString(),
            ["direction"] = direction.ToString().ToLowerInvariant(),
            ["maxHops"] = maxHops,
            ["typesFilter"] = typesNode,
            ["hops"] = hopsArray,
            ["visitedCount"] = result.Visited.Count,
        };

        Console.WriteLine(output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static Direction ParseDirection(string? raw) => raw?.ToLowerInvariant() switch
    {
        null or "outbound" => Direction.Outbound,
        "inbound" => Direction.Inbound,
        "both" => Direction.Both,
        _ => throw new ArgumentException($"--to must be one of outbound|inbound|both (got '{raw}')."),
    };

    private static IReadOnlyCollection<RelationshipType>? ParseTypes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parsed = new List<RelationshipType>();
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            parsed.Add(RelationshipTypeWireForCli.Parse(token));
        }

        return parsed;
    }
}

// Wire-format helper duplicated from the persistence layer (internal there) to
// keep the CLI standalone. Spec value matches contracts/relationship-types.md.
internal static class RelationshipTypeWireForCli
{
    public static string Of(RelationshipType type) => type switch
    {
        RelationshipType.PublishesTo => "publishesTo",
        RelationshipType.ConsumedBy => "consumedBy",
        RelationshipType.SubscriptionOf => "subscriptionOf",
        RelationshipType.UsesContract => "usesContract",
        RelationshipType.Owns => "owns",
        RelationshipType.AttachedTo => "attachedTo",
        RelationshipType.Replaces => "replaces",
        RelationshipType.PartOfFlow => "partOfFlow",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown RelationshipType."),
    };

    public static RelationshipType Parse(string raw) =>
        raw.ToLower(CultureInfo.InvariantCulture) switch
        {
            "publishesto" => RelationshipType.PublishesTo,
            "consumedby" => RelationshipType.ConsumedBy,
            "subscriptionof" => RelationshipType.SubscriptionOf,
            "usescontract" => RelationshipType.UsesContract,
            "owns" => RelationshipType.Owns,
            "attachedto" => RelationshipType.AttachedTo,
            "replaces" => RelationshipType.Replaces,
            "partofflow" => RelationshipType.PartOfFlow,
            _ => throw new ArgumentException($"Unknown relationship type '{raw}'. Valid: publishesTo, consumedBy, subscriptionOf, usesContract, owns, attachedTo, replaces, partOfFlow."),
        };
}
