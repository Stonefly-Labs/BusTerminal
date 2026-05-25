using System.Text.Json;
using System.Text.Json.Nodes;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T097 / FR-009 / quickstart Smoke 1. Print the structured ownership block for a
// resource — team id, technical/business contacts, escalation, support, and
// operational tier — and follow up with a lookup against the Team resource so
// the operator sees the team's display name rather than just a Guid.
internal static class ShowOwnerCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResourceId))
        {
            Console.Error.WriteLine("show-owner: --resource-id is required.");
            return 64;
        }

        var id = ResourceId.Parse(options.ResourceId);
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var registry = services.GetRequiredService<ResourceTypeRegistry>();
        var serializer = services.GetRequiredService<JsonResourceSerializer>();

        Resource? resource = null;
        foreach (var discriminator in registry.KnownDiscriminators)
        {
            resource = await store.GetAsync(id, discriminator, options.IncludeDeleted, cancellationToken).ConfigureAwait(false);
            if (resource is not null)
            {
                break;
            }
        }

        if (resource is null)
        {
            Console.Error.WriteLine($"show-owner: no resource found with id {id}.");
            return 1;
        }

        if (resource.Ownership is null)
        {
            Console.Error.WriteLine(
                $"show-owner: resource {id} ('{resource.ResourceType}') has no ownership block. " +
                "Only operational types carry ownership.");
            return 1;
        }

        var teamLookup = await store.GetAsync(
            resource.Ownership.OwningTeamId,
            ResourceTypeDiscriminators.Team,
            includeDeleted: false,
            cancellationToken).ConfigureAwait(false);

        var ownershipJson = JsonNode.Parse(JsonSerializer.Serialize(resource.Ownership, serializer.Options))!;

        var output = new JsonObject
        {
            ["resourceId"] = resource.Id.ToString(),
            ["resourceType"] = resource.ResourceType,
            ["displayName"] = resource.DisplayName,
            ["ownership"] = ownershipJson,
            ["owningTeam"] = teamLookup is Team team
                ? new JsonObject
                {
                    ["id"] = team.Id.ToString(),
                    ["name"] = team.Name.Value,
                    ["displayName"] = team.DisplayName,
                    ["exists"] = true,
                }
                : new JsonObject
                {
                    ["id"] = resource.Ownership.OwningTeamId.ToString(),
                    ["exists"] = false,
                    ["note"] = "Team referent does not resolve in the canonical store (dangling reference).",
                },
        };

        Console.WriteLine(output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
