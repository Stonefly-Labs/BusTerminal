using System.Net;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Tools.DiscoveryLockReset;

// Spec 009 / T125a + quickstart "Useful one-liners". Resets the
// `discovery-locks` document for a single namespace so debug / recovery
// flows can free a stuck lock without round-tripping through the API.
//
// USAGE:
//   busterminal-discovery-lock-reset \
//     --endpoint https://bt-<env>-cosmos.documents.azure.com:443/ \
//     --namespace-id ns_01HKXP... \
//     [--database canonical] [--container discovery-locks]
//
// AUTH: `DefaultAzureCredential` — assumes the operator is signed in via
// `az login` OR the workload identity. The CLI does NOT accept connection
// strings or master keys (constitution: managed identity preferred,
// secret-free).
//
// SAFETY:
//   * Read first, log the prior lock state to stdout.
//   * Then write a "released" sentinel: currentRunId=null, acquiredByPodId=null,
//     expectedReleaseByUtc=null. Honors the Cosmos ETag of the read response.
//   * No --force option. If a concurrent run grabbed the lock between the
//     read and the write, the 412 surfaces as a non-zero exit so the
//     operator can re-evaluate before stomping.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Options opts;
        try
        {
            opts = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"busterminal-discovery-lock-reset: {ex.Message}");
            PrintUsage();
            return 64;
        }

        if (opts.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        try
        {
            var credential = new DefaultAzureCredential();
            using var client = new CosmosClient(opts.Endpoint, credential, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationName = "busterminal-discovery-lock-reset",
            });

            var container = client.GetContainer(opts.Database, opts.Container);
            var partitionKey = new PartitionKey(opts.NamespaceId);

            ItemResponse<LockDocument> read;
            try
            {
                read = await container.ReadItemAsync<LockDocument>("lock", partitionKey).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"no lock document exists for namespace {opts.NamespaceId}; nothing to reset.");
                return 0;
            }

            Console.WriteLine($"current lock state for namespace {opts.NamespaceId}:");
            Console.WriteLine($"  currentRunId        = {read.Resource.CurrentRunId ?? "(null)"}");
            Console.WriteLine($"  acquiredByPodId     = {read.Resource.AcquiredByPodId ?? "(null)"}");
            Console.WriteLine($"  expectedReleaseByUtc= {read.Resource.ExpectedReleaseByUtc?.ToString("O") ?? "(null)"}");

            if (opts.ReadOnly)
            {
                return 0;
            }

            var released = read.Resource with
            {
                CurrentRunId = null,
                AcquiredByPodId = null,
                ExpectedReleaseByUtc = null,
            };

            try
            {
                await container.ReplaceItemAsync(
                    released,
                    id: "lock",
                    partitionKey: partitionKey,
                    requestOptions: new ItemRequestOptions { IfMatchEtag = read.ETag }).ConfigureAwait(false);
                Console.WriteLine("lock released.");
                return 0;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                Console.Error.WriteLine("lock changed between read and write (412). Re-run to retry, or investigate the in-flight run.");
                return 75;
            }
        }
        catch (CosmosException ex)
        {
            Console.Error.WriteLine($"Cosmos error: {ex.StatusCode} {ex.Message}");
            return 70;
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException)
        {
            Console.Error.WriteLine($"busterminal-discovery-lock-reset: {ex.Message}");
            return 70;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            busterminal-discovery-lock-reset — clear a per-namespace discovery lock for debug / recovery.

            Usage:
              busterminal-discovery-lock-reset --endpoint <url> --namespace-id <ns> [--database <db>] [--container <name>] [--read-only]

            Options:
              --endpoint <url>          Cosmos account endpoint (required).
              --namespace-id <ns>       Partition key of the lock document (required).
              --database <db>           Database name (default: canonical).
              --container <name>        Container name (default: discovery-locks).
              --read-only               Print the current state and exit without modifying.
              --help, -h                Print this help.

            Auth: DefaultAzureCredential. Run `az login` (or rely on a workload identity) before invoking.

            Exit codes: 0 success, 64 bad usage, 70 transient, 75 concurrent change (412 — re-run).
            """);
    }
}

internal sealed record Options(
    string Endpoint,
    string NamespaceId,
    string Database,
    string Container,
    bool ReadOnly,
    bool ShowHelp)
{
    public static Options Parse(string[] args)
    {
        string? endpoint = null;
        string? namespaceId = null;
        var database = "canonical";
        var container = "discovery-locks";
        var readOnly = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--endpoint":
                    endpoint = NextValue(args, ref i, "--endpoint");
                    break;
                case "--namespace-id":
                    namespaceId = NextValue(args, ref i, "--namespace-id");
                    break;
                case "--database":
                    database = NextValue(args, ref i, "--database");
                    break;
                case "--container":
                    container = NextValue(args, ref i, "--container");
                    break;
                case "--read-only":
                    readOnly = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }

        if (showHelp)
        {
            return new Options(endpoint ?? string.Empty, namespaceId ?? string.Empty, database, container, readOnly, ShowHelp: true);
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("--endpoint is required");
        }
        if (string.IsNullOrWhiteSpace(namespaceId))
        {
            throw new ArgumentException("--namespace-id is required");
        }

        return new Options(endpoint, namespaceId, database, container, readOnly, ShowHelp: false);
    }

    private static string NextValue(string[] args, ref int i, string flag)
    {
        i++;
        if (i >= args.Length)
        {
            throw new ArgumentException($"{flag} requires a value");
        }
        return args[i];
    }
}

internal sealed record LockDocument
{
    [System.Text.Json.Serialization.JsonPropertyName("id")] public string Id { get; init; } = "lock";
    [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")] public string SchemaVersion { get; init; } = "1.0";
    [System.Text.Json.Serialization.JsonPropertyName("namespaceId")] public string NamespaceId { get; init; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("currentRunId")] public string? CurrentRunId { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("acquiredUtc")] public DateTimeOffset? AcquiredUtc { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("acquiredByPodId")] public string? AcquiredByPodId { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("expectedReleaseByUtc")] public DateTimeOffset? ExpectedReleaseByUtc { get; init; }
}
