namespace BusTerminal.Tools.LoadFixtures;

// Scaffold for the BusTerminal canonical-store fixture-load CLI.
// Subcommands are stubbed; bodies are filled in by later spec-004 tasks
// (T079, T080, T081, T082, T097, T109, T124, T125, T143, T144, T145).

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var verb = args[0].ToLowerInvariant();
        var verbArgs = args.AsSpan(1).ToArray();

        return verb switch
        {
            "create-database" => NotImplemented(verb, "T079"),
            "import" => NotImplemented(verb, "T080"),
            "export" => NotImplemented(verb, "T143"),
            "show" => NotImplemented(verb, "T081"),
            "show-owner" => NotImplemented(verb, "T097"),
            "traverse" => NotImplemented(verb, "T109"),
            "transition" => NotImplemented(verb, "T124"),
            "soft-delete" => NotImplemented(verb, "T124"),
            "restore" => NotImplemented(verb, "T124"),
            "changelog" => NotImplemented(verb, "T125"),
            "truncate" => NotImplemented(verb, "T082"),
            "--help" or "-h" or "help" => Help(),
            _ => Unknown(verb),
        };
    }

    private static int NotImplemented(string verb, string ownerTask)
    {
        Console.Error.WriteLine($"busterminal-load-fixtures: '{verb}' is not yet implemented (owner task: {ownerTask}).");
        return 64; // EX_USAGE-ish
    }

    private static int Unknown(string verb)
    {
        Console.Error.WriteLine($"busterminal-load-fixtures: unknown verb '{verb}'.");
        PrintUsage();
        return 64;
    }

    private static int Help()
    {
        PrintUsage();
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            busterminal-load-fixtures — BusTerminal canonical-store fixture and operations CLI.

            Usage:
              busterminal-load-fixtures <verb> [options]

            Verbs:
              create-database     Create the canonical database and containers (idempotent).
              import              Import an envelope or directory of envelopes.
              export              Export the canonical store to a portable envelope.
              show                Show a single resource by id.
              show-owner          Show the structured ownership block for a resource.
              traverse            Traverse the relationship graph from a starting resource.
              transition          Move a resource to a new lifecycle state.
              soft-delete         Mark a resource as deleted (preserves identifier + audit + change log).
              restore             Restore a soft-deleted resource to its prior lifecycle state.
              changelog           Show the ordered change-event log for a resource.
              truncate            Delete all documents from both canonical containers.

            Subcommand options and behavior land in later spec-004 implementation tasks.
            """);
    }
}
