using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T080 — load envelopes into the canonical store. Supports --fixtures-dir (all
// *.json under the dir, lexicographic order) and --fixtures (single file).
// Relationships in the envelope are skipped with an Info log until US3 T104
// extends the store with relationship CRUD.
//
// Output line (per spec): "Loaded N resources, M relationships. Findings:
// E Error, W Warning, I Info (X resources without any finding)."
// A resource with Warning/Info still loads; only Error rejects (FR-013).
internal static class ImportCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var files = ResolveFiles(options);
        if (files.Count == 0)
        {
            Console.Error.WriteLine("import: provide --fixtures-dir <path> or --fixtures <file> or --input <file>.");
            return 64;
        }

        var serializer = services.GetRequiredService<JsonResourceSerializer>();
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var engine = services.GetRequiredService<ValidationEngine>();

        var loadedResources = 0;
        var loadedRelationships = 0;
        var rejected = 0;
        var errorFindings = 0;
        var warningFindings = 0;
        var infoFindings = 0;
        var resourcesWithNoFinding = 0;

        foreach (var file in files)
        {
            var envelope = serializer.DeserializeEnvelopeFromJson(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));

            foreach (var resource in envelope.Resources)
            {
                var validation = await engine.ValidateAsync(
                    resource,
                    relationshipResolver: _ => null, // No relationship resolver in US1; DanglingReferenceRule lands in US3.
                    duplicateDetector: _ => false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var stamped = resource with { ValidationState = validation };

                if (validation.HasErrors)
                {
                    rejected++;
                    foreach (var f in validation.Findings)
                    {
                        Console.Error.WriteLine($"[rejected] {resource.Id} ({resource.ResourceType}) — {f.Severity}: {f.Message}");
                    }

                    CountFindings(validation, ref errorFindings, ref warningFindings, ref infoFindings);
                    continue;
                }

                CountFindings(validation, ref errorFindings, ref warningFindings, ref infoFindings);
                if (validation.Findings.Count == 0)
                {
                    resourcesWithNoFinding++;
                }

                await store.CreateAsync(stamped, ServiceHost.Actor, ServiceHost.SourceSystem, cancellationToken).ConfigureAwait(false);
                loadedResources++;
            }

            foreach (var relationship in envelope.Relationships)
            {
                await store.CreateRelationshipAsync(
                    relationship,
                    ServiceHost.Actor,
                    ServiceHost.SourceSystem,
                    cancellationToken).ConfigureAwait(false);
                loadedRelationships++;
            }
        }

        Console.WriteLine(
            $"Loaded {loadedResources} resources, {loadedRelationships} relationships. " +
            $"Findings: {errorFindings} Error, {warningFindings} Warning, {infoFindings} Info " +
            $"({resourcesWithNoFinding} resources without any finding).");

        if (rejected > 0)
        {
            Console.Error.WriteLine($"Rejected {rejected} resource(s) with Error findings.");
            return 1;
        }

        return 0;
    }

    private static IReadOnlyList<string> ResolveFiles(CliOptions options)
    {
        if (options.FixturesDir is not null)
        {
            return [.. Directory.EnumerateFiles(options.FixturesDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)];
        }

        if (options.FixturesFile is not null)
        {
            return [options.FixturesFile];
        }

        if (options.Input is not null)
        {
            return [options.Input];
        }

        return [];
    }

    private static void CountFindings(
        ValidationResult result,
        ref int error,
        ref int warning,
        ref int info)
    {
        foreach (var finding in result.Findings)
        {
            switch (finding.Severity)
            {
                case ValidationSeverity.Error: error++; break;
                case ValidationSeverity.Warning: warning++; break;
                case ValidationSeverity.Info: info++; break;
            }
        }
    }
}
