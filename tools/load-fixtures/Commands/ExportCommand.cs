using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// Spec 004 / FR-016 / T143 (US8). Export the canonical store to a JSON or YAML
// envelope. Drives SC-009 (lossless round-trip) and the disaster-recovery /
// backup story.
//
// Flags:
//   --output <path>              required. Where to write the envelope.
//   --format json|yaml           defaults to json.
//   --include-deleted            include soft-deleted resources (excluded by default).
//   --include-change-log         attach every ChangeEvent across the store.
//
// Exit codes follow the rest of the CLI: 0 success, 64 usage error.
internal static class ExportCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Output))
        {
            Console.Error.WriteLine("export: --output <path> is required.");
            return 64;
        }

        var format = options.Format?.ToLowerInvariant() ?? "json";
        if (format is not "json" and not "yaml")
        {
            Console.Error.WriteLine($"export: --format must be 'json' or 'yaml' (got '{options.Format}').");
            return 64;
        }

        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var changeLog = services.GetRequiredService<IChangeEventLog>();
        var json = services.GetRequiredService<JsonResourceSerializer>();
        var yaml = services.GetRequiredService<YamlResourceSerializer>();

        var resources = new List<Resource>();
        await foreach (var r in store.QueryAsync(
            new ResourceQuery.All(ResourceTypeDiscriminator: null, IncludeDeleted: options.IncludeDeleted),
            cancellationToken).ConfigureAwait(false))
        {
            resources.Add(r);
        }

        var relationships = new List<Relationship>();
        await foreach (var rel in store.QueryRelationshipsAsync(
            new RelationshipQuery.All(IncludeDeleted: options.IncludeDeleted),
            cancellationToken).ConfigureAwait(false))
        {
            relationships.Add(rel);
        }

        List<ChangeEvent>? changeEvents = null;
        if (options.IncludeChangeLog)
        {
            changeEvents = [];
            await foreach (var evt in changeLog.QueryAllAsync(cancellationToken).ConfigureAwait(false))
            {
                changeEvents.Add(evt);
            }
        }

        var envelope = new ImportExportEnvelope(
            ExportedAt: DateTimeOffset.UtcNow,
            Resources: resources,
            Relationships: relationships,
            ExportedBy: ServiceHost.Actor,
            SourceSystem: ServiceHost.SourceSystem,
            ChangeEvents: changeEvents);

        var serialized = format == "yaml"
            ? yaml.SerializeEnvelopeToYaml(envelope)
            : json.SerializeEnvelopeToJson(envelope);

        var outputPath = options.Output!;
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, serialized, cancellationToken).ConfigureAwait(false);

        Console.WriteLine(
            $"Exported {resources.Count} resources, {relationships.Count} relationships" +
            (changeEvents is null ? "" : $", {changeEvents.Count} change events") +
            $" to {outputPath} ({format}).");

        return 0;
    }
}
