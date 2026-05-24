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
        "truncate" => TruncateCommand.RunAsync(services, cancellationToken),

        // Verbs landing in later user-story phases.
        "show-owner" => NotImplemented(verb, "T097 (US2)"),
        "traverse" => NotImplemented(verb, "T109 (US3)"),
        "transition" => NotImplemented(verb, "T124 (US5)"),
        "soft-delete" => NotImplemented(verb, "T124 (US5)"),
        "restore" => NotImplemented(verb, "T124 (US5)"),
        "changelog" => NotImplemented(verb, "T125 (US5)"),
        "export" => NotImplemented(verb, "T143 (US8)"),

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
              truncate                Delete every document from both canonical containers.

            Verbs (deferred to later user stories):
              show-owner              US2 / T097.
              traverse                US3 / T109.
              transition              US5 / T124.
              soft-delete, restore    US5 / T124.
              changelog               US5 / T125.
              export                  US8 / T143.
            """);
    }
}
