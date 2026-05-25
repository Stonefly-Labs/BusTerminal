namespace BusTerminal.Tools.LoadFixtures;

// Flat option bag parsed from argv. Includes both verb-style ("verb [flags]")
// and quickstart-style flag-as-action invocations (--create-database,
// --fixtures-dir <path>).
internal sealed record CliOptions(
    string Verb,
    string? Endpoint,
    string Auth,
    string? FixturesDir,
    string? FixturesFile,
    string? ResourceId,
    string Format,
    bool IncludeDeleted,
    string? Output,
    string? Input,
    string? ConflictResolution,
    string? To,
    int? MaxHops,
    string? Types,
    bool IncludeChangeLog)
{
    public const string AuthEmulatorKey = "emulator-key";
    public const string AuthAad = "aad";

    public static CliOptions Parse(string[] args)
    {
        string? verb = null;
        string? endpoint = null;
        var auth = AuthEmulatorKey;
        string? fixturesDir = null;
        string? fixturesFile = null;
        string? resourceId = null;
        var format = "json";
        var includeDeleted = false;
        string? output = null;
        string? input = null;
        string? conflictResolution = null;
        string? to = null;
        int? maxHops = null;
        string? types = null;
        var includeChangeLog = false;

        // Quickstart-style action flags (no verb required when these are set).
        var createDatabaseFlag = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--endpoint":
                    endpoint = NextValue(args, ref i, arg);
                    break;
                case "--auth":
                    auth = NextValue(args, ref i, arg);
                    break;
                case "--fixtures-dir":
                    fixturesDir = NextValue(args, ref i, arg);
                    break;
                case "--fixtures":
                    fixturesFile = NextValue(args, ref i, arg);
                    break;
                case "--resource-id":
                    resourceId = NextValue(args, ref i, arg);
                    break;
                case "--format":
                    format = NextValue(args, ref i, arg);
                    break;
                case "--include-deleted":
                    includeDeleted = true;
                    break;
                case "--output":
                    output = NextValue(args, ref i, arg);
                    break;
                case "--input":
                    input = NextValue(args, ref i, arg);
                    break;
                case "--conflict-resolution":
                    conflictResolution = NextValue(args, ref i, arg);
                    break;
                case "--to":
                    to = NextValue(args, ref i, arg);
                    break;
                case "--max-hops":
                    maxHops = int.Parse(NextValue(args, ref i, arg), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--types":
                    types = NextValue(args, ref i, arg);
                    break;
                case "--include-change-log":
                    includeChangeLog = true;
                    break;
                case "--from":
                    // --from is an alias for --resource-id used by the `traverse`
                    // verb per quickstart Smoke 2.
                    resourceId = NextValue(args, ref i, arg);
                    break;
                case "--create-database":
                    createDatabaseFlag = true;
                    break;
                case "--help" or "-h" or "help":
                    verb ??= "help";
                    break;
                default:
                    if (!arg.StartsWith("--", StringComparison.Ordinal) && verb is null)
                    {
                        verb = arg;
                    }
                    else if (!arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unexpected positional argument: '{arg}'.");
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown flag: '{arg}'.");
                    }

                    break;
            }
        }

        // Quickstart flag-as-action translation: when no verb is given but a
        // distinguishing flag is, infer the verb.
        if (verb is null)
        {
            if (createDatabaseFlag)
            {
                verb = "create-database";
            }
            else if (fixturesDir is not null || fixturesFile is not null || input is not null)
            {
                verb = "import";
            }
            else
            {
                verb = "help";
            }
        }

        return new CliOptions(
            Verb: verb,
            Endpoint: endpoint,
            Auth: auth,
            FixturesDir: fixturesDir,
            FixturesFile: fixturesFile,
            ResourceId: resourceId,
            Format: format,
            IncludeDeleted: includeDeleted,
            Output: output,
            Input: input,
            ConflictResolution: conflictResolution,
            To: to,
            MaxHops: maxHops,
            Types: types,
            IncludeChangeLog: includeChangeLog);
    }

    private static string NextValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Flag '{flag}' requires a value.");
        }

        return args[++i];
    }
}
