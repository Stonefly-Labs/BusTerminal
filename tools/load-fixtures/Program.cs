using System.Diagnostics.CodeAnalysis;
using BusTerminal.Tools.LoadFixtures.Commands;

namespace BusTerminal.Tools.LoadFixtures;

// BusTerminal canonical-store fixture-load CLI. Hybrid verb + flag-as-action
// dispatcher to honor the spec quickstart (Path A uses flag style; smoke
// validations use verb style).
internal static class Program
{
    // Top-level catch-all is intentional: a CLI exit code is more useful to
    // operators than an unhandled-exception stack trace.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Top-level CLI handler must convert any failure to an exit code.")]
    private static async Task<int> Main(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"busterminal-load-fixtures: {ex.Message}");
            PrintUsage();
            return 64;
        }

        if (options.Verb is "help" or "--help" or "-h")
        {
            PrintUsage();
            return 0;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            await using var services = ServiceHost.Build(options);
            return await DispatchAsync(options.Verb, services, options, cancellation.Token).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"busterminal-load-fixtures: {ex.Message}");
            return 64;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"busterminal-load-fixtures: unexpected error: {ex.Message}");
            return 70;
        }
    }

    private static Task<int> DispatchAsync(
        string verb,
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken) => verb switch
    {
        "create-database" => CreateDatabaseCommand.RunAsync(services, cancellationToken),
        "import" => ImportCommand.RunAsync(services, options, cancellationToken),
        "show" => ShowCommand.RunAsync(services, options, cancellationToken),
        "show-owner" => ShowOwnerCommand.RunAsync(services, options, cancellationToken),
        "truncate" => TruncateCommand.RunAsync(services, cancellationToken),
        "traverse" => TraverseCommand.RunAsync(services, options, cancellationToken),

        // Spec 004 / US5 / T124 + T125.
        "transition" => TransitionCommand.RunAsync(services, options, cancellationToken),
        "soft-delete" => SoftDeleteCommand.RunAsync(services, options, cancellationToken),
        "restore" => RestoreCommand.RunAsync(services, options, cancellationToken),
        "changelog" => ChangelogCommand.RunAsync(services, options, cancellationToken),

        // Spec 004 / US8 / T143.
        "export" => ExportCommand.RunAsync(services, options, cancellationToken),

        _ => Unknown(verb),
    };

    private static Task<int> NotImplemented(string verb, string ownerTask)
    {
        Console.Error.WriteLine($"busterminal-load-fixtures: '{verb}' is not yet implemented (owner task: {ownerTask}).");
        return Task.FromResult(64);
    }

    private static Task<int> Unknown(string verb)
    {
        Console.Error.WriteLine($"busterminal-load-fixtures: unknown verb '{verb}'.");
        PrintUsage();
        return Task.FromResult(64);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            busterminal-load-fixtures — BusTerminal canonical-store fixture and operations CLI.

            Usage:
              busterminal-load-fixtures <verb> [options]
              busterminal-load-fixtures [options]   (flag-style — verb inferred from --create-database / --fixtures-dir / --input)

            Common options:
              --endpoint <url>        Cosmos account endpoint (required for everything except `help`).
              --auth <mode>           emulator-key (default, dev only) | aad (DefaultAzureCredential).

            Verbs (Phase 3 — Spec 004 US1):
              create-database         Create the canonical database + containers (idempotent).
              import                  Load envelope(s) into the store. Use --fixtures-dir or --fixtures.
              show                    Print a single resource by id. Use --resource-id [--include-deleted] [--format json].
              show-owner              Print the structured ownership block for a resource + resolved Team display name. Use --resource-id.
              truncate                Delete every document from both canonical containers.

            Verbs (Phase 5 — Spec 004 US3):
              traverse                Traverse the relationship graph from a resource. --from <id> [--max-hops N] [--to outbound|inbound|both] [--types publishesTo,owns,...].

            Verbs (Phase 7 — Spec 004 US5):
              transition              Move a resource to a new Lifecycle. --resource-id <id> --to draft|active|deprecated|retired|archived.
              soft-delete             Set IsDeleted=true on a resource. --resource-id <id>.
              restore                 Clear IsDeleted on a soft-deleted resource. --resource-id <id>.
              changelog               Print the ordered change-event log for a resource. --resource-id <id>.

            Verbs (Phase 10 — Spec 004 US8):
              export                  Export the canonical store. --output <path> [--format json|yaml] [--include-deleted] [--include-change-log].
              import                  Supports --input <file> and --conflict-resolution reject|skip|overwrite (default reject).
              show                    Supports --format yaml in addition to json.
            """);
    }
}
