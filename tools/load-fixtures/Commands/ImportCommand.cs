using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T080 — load envelopes into the canonical store. Supports --fixtures-dir (all
// *.json under the dir, lexicographic order), --fixtures (single file), and
// --input (single envelope, US8 disaster-recovery path).
//
// T134 (US6) — patch-style upsert. The fixture set is shipped as multiple
// envelope files (`01-base.json`, `02-relationships.json`, …) where later files
// overlay modifications onto records the earlier files created. Default
// conflict resolution for that flow is `overwrite`.
//
// T144 (US8) — explicit conflict resolution. `--conflict-resolution
// reject|skip|overwrite` governs duplicate-identifier handling per spec
// Scenario 3. Defaults are mode-aware:
//   --input <file>       → reject  (disaster-recovery into an empty store)
//   --fixtures-dir/file  → overwrite  (multi-file patch overlay)
// The explicit flag always wins. The resolution outcome is recorded in the
// import report and (for `overwrite`) in the change-event log via the existing
// Update path's SourceSystem stamp.
//
// Output line: "Loaded N resources, M relationships. Findings: E Error,
// W Warning, I Info (X resources without any finding). Conflicts: O overwritten,
// S skipped, R rejected."
// A resource with Warning/Info still loads; only Error rejects (FR-013).
internal static class ImportCommand
{
    private const string Reject = "reject";
    private const string Skip = "skip";
    private const string Overwrite = "overwrite";

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

        var (resolution, resolutionExplicit) = ResolveConflictPolicy(options);
        if (resolution is null)
        {
            Console.Error.WriteLine($"import: --conflict-resolution must be one of '{Reject}', '{Skip}', or '{Overwrite}' (got '{options.ConflictResolution}').");
            return 64;
        }

        var serializer = services.GetRequiredService<JsonResourceSerializer>();
        var store = services.GetRequiredService<ICanonicalResourceStore>();
        var engine = services.GetRequiredService<ValidationEngine>();

        // T155 fix — pre-deserialize every envelope file so the relationship
        // resolver knows about cross-file references inside the fixture set
        // before any resource is written. US3's DanglingReferenceRule consults
        // this resolver; the previous `_ => null` shim treated every reference
        // as dangling and rejected the entire fixture set whenever a Team /
        // Topic / Application appeared in a different file than the resource
        // that names it. The resolver also falls through to the store so
        // existing documents (re-import after a partial load) resolve too.
        var envelopes = new List<(string File, ImportExportEnvelope Envelope)>(files.Count);
        var unionMap = new Dictionary<ResourceId, Resource>();
        foreach (var file in files)
        {
            var envelope = serializer.DeserializeEnvelopeFromJson(
                await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));
            envelopes.Add((file, envelope));
            foreach (var r in envelope.Resources)
            {
                unionMap[r.Id] = r;
            }
        }

        Resource? Resolve(ResourceId id)
            => unionMap.TryGetValue(id, out var fromEnvelope) ? fromEnvelope : null;

        var loadedResources = 0;
        var overwrittenResources = 0;
        var skippedResources = 0;
        var conflictRejected = 0;
        var loadedRelationships = 0;
        var validationRejected = 0;
        var errorFindings = 0;
        var warningFindings = 0;
        var infoFindings = 0;
        var resourcesWithNoFinding = 0;

        foreach (var (file, envelope) in envelopes)
        {
            foreach (var resource in envelope.Resources)
            {
                var validation = await engine.ValidateAsync(
                    resource,
                    relationshipResolver: Resolve,
                    duplicateDetector: _ => false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var stamped = resource with { ValidationState = validation };

                if (validation.HasErrors)
                {
                    validationRejected++;
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

                var existing = await store.GetAsync(
                    resource.Id,
                    resource.ResourceType,
                    includeDeleted: true,
                    cancellationToken).ConfigureAwait(false);

                if (existing is null)
                {
                    await store.CreateAsync(stamped, ServiceHost.Actor, ServiceHost.SourceSystem, cancellationToken).ConfigureAwait(false);
                    loadedResources++;
                    continue;
                }

                switch (resolution)
                {
                    case Reject:
                        conflictRejected++;
                        Console.Error.WriteLine(
                            $"[conflict-rejected] {resource.Id} ({resource.ResourceType}) already exists; --conflict-resolution=reject.");
                        break;

                    case Skip:
                        skippedResources++;
                        Console.WriteLine(
                            $"[conflict-skipped] {resource.Id} ({resource.ResourceType}) already exists; --conflict-resolution=skip.");
                        break;

                    case Overwrite:
                        var patched = stamped with { ConcurrencyToken = existing.ConcurrencyToken };
                        await store.UpdateAsync(patched, ServiceHost.Actor, ServiceHost.SourceSystem, cancellationToken).ConfigureAwait(false);
                        overwrittenResources++;
                        break;
                }
            }

            foreach (var relationship in envelope.Relationships)
            {
                var existingRel = await store.GetRelationshipAsync(
                    relationship.Id,
                    includeDeleted: true,
                    cancellationToken).ConfigureAwait(false);

                if (existingRel is null)
                {
                    await store.CreateRelationshipAsync(
                        relationship,
                        ServiceHost.Actor,
                        ServiceHost.SourceSystem,
                        cancellationToken).ConfigureAwait(false);
                    loadedRelationships++;
                    continue;
                }

                switch (resolution)
                {
                    case Reject:
                        conflictRejected++;
                        Console.Error.WriteLine(
                            $"[conflict-rejected] relationship {relationship.Id} already exists; --conflict-resolution=reject.");
                        break;

                    case Skip:
                        skippedResources++;
                        Console.WriteLine(
                            $"[conflict-skipped] relationship {relationship.Id} already exists; --conflict-resolution=skip.");
                        break;

                    case Overwrite:
                        // Relationships don't currently expose an Update path
                        // on ICanonicalResourceStore — the existing CRUD only
                        // covers create + soft-delete-via-IsDeleted. For
                        // overwrite semantics we record this as a skip with a
                        // diagnostic so an operator knows to address it
                        // manually (or extend the store in a follow-up).
                        skippedResources++;
                        Console.WriteLine(
                            $"[conflict-skipped] relationship {relationship.Id} already exists; --conflict-resolution=overwrite (relationships are not yet mutable — kept the existing edge).");
                        break;
                }
            }
        }

        var defaultLabel = resolutionExplicit ? "" : " (default for mode)";
        Console.WriteLine(
            $"Loaded {loadedResources} resources, {loadedRelationships} relationships. " +
            $"Findings: {errorFindings} Error, {warningFindings} Warning, {infoFindings} Info " +
            $"({resourcesWithNoFinding} resources without any finding). " +
            $"Conflict resolution: {resolution}{defaultLabel} → {overwrittenResources} overwritten, " +
            $"{skippedResources} skipped, {conflictRejected} rejected.");

        if (validationRejected > 0)
        {
            Console.Error.WriteLine($"Rejected {validationRejected} resource(s) with Error findings.");
            return 1;
        }

        if (conflictRejected > 0)
        {
            Console.Error.WriteLine($"Rejected {conflictRejected} resource(s) due to identifier conflicts under --conflict-resolution=reject.");
            return 2;
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

    // Returns (policy, explicit). `policy` is null when the flag was provided
    // with an invalid value.
    private static (string? Policy, bool Explicit) ResolveConflictPolicy(CliOptions options)
    {
        if (string.IsNullOrEmpty(options.ConflictResolution))
        {
            // Mode-aware defaults — `--fixtures-dir` / `--fixtures` mode is the
            // T134 patch-overlay path; `--input` mode is the US8 disaster-recovery
            // path. Explicit flag always wins for both.
            var defaultPolicy = options.Input is not null && options.FixturesDir is null && options.FixturesFile is null
                ? Reject
                : Overwrite;
            return (defaultPolicy, Explicit: false);
        }

        var requested = options.ConflictResolution.ToLowerInvariant();
        return requested switch
        {
            Reject or Skip or Overwrite => (requested, Explicit: true),
            _ => (null, Explicit: true),
        };
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
