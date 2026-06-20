using System.Text.RegularExpressions;

namespace BusTerminal.Indexer.Discovery;

// Spec 009 / T053a. Strips potentially-sensitive fragments (ARM resource
// paths, namespace + entity names) from exception messages before they're
// persisted to `DiscoveryRun.failure.message` or stamped on a span. Enforces
// the constitution's "no PII in telemetry" rule and the R-12 dimension cap.
//
// Behavior:
//   - `/subscriptions/{guid}/resourceGroups/{name}/...` → `(redacted-arm-id)`
//   - `subscriptionId=<guid>` / `objectId=<guid>` pairs → `(redacted-guid)`
//   - Empty/whitespace input → `"(redacted)"`
//   - Anything > 2 KB → truncated to 2 KB + suffix
public static partial class FailureMessageSanitizer
{
    public const string Fallback = "(redacted)";
    public const int MaxLength = 2048;

    [GeneratedRegex(@"/subscriptions/[0-9a-fA-F\-]{8,}/resourceGroups/[^/\s""']+(/[^?\s""']+)*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArmIdRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex GuidRegex();

    public static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Fallback;
        }
        var redacted = ArmIdRegex().Replace(message, "(redacted-arm-id)");
        redacted = GuidRegex().Replace(redacted, "(redacted-guid)");
        // The Service Bus / Cosmos SDKs sometimes echo the entity name in
        // messages like `Entity 'orders-inbox' could not be ...`. We collapse
        // any single-quoted run between letters/digits/hyphens to a placeholder
        // — false positives are acceptable; we err on the side of redaction.
        redacted = Regex.Replace(redacted, "'[A-Za-z0-9_\\-./]{1,100}'", "'(redacted-name)'", RegexOptions.CultureInvariant);

        if (redacted.Length > MaxLength)
        {
            return redacted.Substring(0, MaxLength) + "… (truncated)";
        }
        return redacted;
    }
}
