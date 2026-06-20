using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BusTerminal.Tools.DiscoveryTelemetryTail;

// Spec 009 / T125b + quickstart "Useful one-liners". Streams discovery
// telemetry to the console for live debug. Two run modes:
//
//  --mode tail       Default. Subscribes to the `BusTerminal.Discovery`
//                    ActivitySource + Meter and echoes everything to
//                    the console as it happens (via the OTel console
//                    exporter).
//
//  --mode otlp       Forwards the same events to a remote OTLP endpoint
//                    instead of the console. `--otlp-endpoint` required.
//                    Useful for piping into a local Jaeger / Tempo / collector.
//
// USAGE:
//   busterminal-discovery-telemetry-tail [--mode tail|otlp] [--otlp-endpoint <url>]
//
// The CLI itself does NOT generate telemetry. It hosts the listener and
// passes through. To see events, the worker / API code under test must
// run in the same process tree OR the CLI must be configured against the
// remote OTLP collector that ingests them.
internal static class Program
{
    public const string DiscoverySource = "BusTerminal.Discovery";

    private static async Task<int> Main(string[] args)
    {
        Options opts;
        try
        {
            opts = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"busterminal-discovery-telemetry-tail: {ex.Message}");
            PrintUsage();
            return 64;
        }

        if (opts.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        // Build the resource describing this process.
        var resource = ResourceBuilder.CreateDefault()
            .AddService("busterminal-discovery-telemetry-tail")
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("debug.tool", "T125b"),
            });

        // Tracer + meter providers.
        using var tracerProvider = BuildTracerProvider(opts, resource);
        using var meterProvider = BuildMeterProvider(opts, resource);

        Console.WriteLine($"listening on ActivitySource={DiscoverySource} Meter={DiscoverySource} (mode={opts.Mode}). Press Ctrl+C to exit.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C.
        }

        return 0;
    }

    private static TracerProvider BuildTracerProvider(Options opts, ResourceBuilder resource)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(DiscoverySource);

        if (string.Equals(opts.Mode, "otlp", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddOtlpExporter(o =>
            {
                if (!string.IsNullOrEmpty(opts.OtlpEndpoint))
                {
                    o.Endpoint = new Uri(opts.OtlpEndpoint);
                }
            });
        }
        else
        {
            builder.AddConsoleExporter();
        }
        return builder.Build()!;
    }

    private static MeterProvider BuildMeterProvider(Options opts, ResourceBuilder resource)
    {
        var builder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddMeter(DiscoverySource);

        if (string.Equals(opts.Mode, "otlp", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddOtlpExporter((o, _) =>
            {
                if (!string.IsNullOrEmpty(opts.OtlpEndpoint))
                {
                    o.Endpoint = new Uri(opts.OtlpEndpoint);
                }
            });
        }
        else
        {
            builder.AddConsoleExporter();
        }
        return builder.Build()!;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            busterminal-discovery-telemetry-tail — stream BusTerminal.Discovery ActivitySource + Meter events.

            Usage:
              busterminal-discovery-telemetry-tail [--mode tail|otlp] [--otlp-endpoint <url>]

            Options:
              --mode <tail|otlp>        tail = console exporter (default). otlp = forward to an OTLP collector.
              --otlp-endpoint <url>     Required when --mode otlp. Example: http://localhost:4317.
              --help, -h                Print this help.

            Caveats:
              - The CLI listens in-process. To see events, run the worker / API
                in the same process tree (e.g. via `dotnet run`) OR point it at
                a remote OTLP collector that already ingests them.
              - Discovery telemetry attributes contain ONLY correlation
                identifiers (namespace id, run id, entity id). No PII.
            """);
    }
}

internal sealed record Options(string Mode, string OtlpEndpoint, bool ShowHelp)
{
    public static Options Parse(string[] args)
    {
        var mode = "tail";
        var otlpEndpoint = string.Empty;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                    mode = NextValue(args, ref i, "--mode");
                    break;
                case "--otlp-endpoint":
                    otlpEndpoint = NextValue(args, ref i, "--otlp-endpoint");
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }

        if (!showHelp && string.Equals(mode, "otlp", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            throw new ArgumentException("--otlp-endpoint is required when --mode otlp");
        }

        return new Options(mode, otlpEndpoint, showHelp);
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

// `Activity` is referenced only to keep the using-import live so the dev tool
// is amenable to extension (e.g. dump tag dictionaries on each activity). The
// pipeline above wires the listener via Sdk.CreateTracerProviderBuilder + AddSource.
internal static class _ActivityKeepAlive
{
    public static Activity? KeepReferenceAlive() => null;
}
