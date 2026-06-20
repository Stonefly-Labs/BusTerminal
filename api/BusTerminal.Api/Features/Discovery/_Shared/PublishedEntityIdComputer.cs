using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T029a / R-07 + FR-009. Stable identity for every PublishedEntity:
//   id = "pe_" + first 24 chars of base32(SHA-256(compositeKey))
//
// The composite key encodes the entity's position in the Service Bus
// hierarchy (queue / topic / subscription / rule) so the same Azure resource
// always hashes to the same id across re-discoveries (idempotency per FR-029).
// Hash truncation chooses 24 base32 chars = 120 bits — sufficient collision
// resistance for the platform's worst-plausible entity count (10⁶+).
public static class PublishedEntityIdComputer
{
    public const string IdPrefix = "pe_";

    // Base32 alphabet per RFC 4648 (no padding). Matches the OpenAPI path
    // pattern `^pe_[A-Z2-7]{24}$`.
    private static readonly char[] Base32Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public static string ComputeFromCompositeKey(string compositeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compositeKey);

        Span<byte> hash = stackalloc byte[32];
        var written = SHA256.HashData(Encoding.UTF8.GetBytes(compositeKey), hash);
        if (written != 32)
        {
            throw new InvalidOperationException("SHA-256 returned unexpected length.");
        }

        return IdPrefix + EncodeBase32Truncated(hash, 24);
    }

    public static string ComposeCompositeKey(
        EntityType entityType,
        string namespaceId,
        string? topicName = null,
        string? subscriptionName = null,
        string? ruleName = null,
        string? leafName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);

        // The leaf name is the entity's own short name (queue name for queues,
        // topic name for topics, etc.). For queues + topics, callers may pass
        // it as leafName OR as topicName for symmetry — we treat the missing
        // one as the leaf.
        return entityType switch
        {
            EntityType.Queue => $"q:{namespaceId}/{RequireLeaf(leafName ?? topicName, nameof(leafName), "queue name")}",
            EntityType.Topic => $"t:{namespaceId}/{RequireLeaf(leafName ?? topicName, nameof(leafName), "topic name")}",
            EntityType.Subscription => $"s:{namespaceId}/{RequireLeaf(topicName, nameof(topicName), "topic name")}/{RequireLeaf(subscriptionName ?? leafName, nameof(subscriptionName), "subscription name")}",
            EntityType.Rule => $"r:{namespaceId}/{RequireLeaf(topicName, nameof(topicName), "topic name")}/{RequireLeaf(subscriptionName, nameof(subscriptionName), "subscription name")}/{RequireLeaf(ruleName ?? leafName, nameof(ruleName), "rule name")}",
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown EntityType."),
        };
    }

    public static string ComputeFor(
        EntityType entityType,
        string namespaceId,
        string? topicName = null,
        string? subscriptionName = null,
        string? ruleName = null,
        string? leafName = null) =>
        ComputeFromCompositeKey(ComposeCompositeKey(entityType, namespaceId, topicName, subscriptionName, ruleName, leafName));

    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith(IdPrefix, StringComparison.Ordinal)) return false;
        var suffix = id.AsSpan(IdPrefix.Length);
        if (suffix.Length != 24) return false;
        foreach (var c in suffix)
        {
            if (!(c is (>= 'A' and <= 'Z') or (>= '2' and <= '7'))) return false;
        }
        return true;
    }

    private static string EncodeBase32Truncated(ReadOnlySpan<byte> bytes, int targetChars)
    {
        // We need targetChars * 5 bits = 120 bits = 15 bytes. SHA-256 gives
        // us 32 bytes — only the first 15 are consumed.
        const int bitsPerChar = 5;
        var totalBits = targetChars * bitsPerChar;
        var bytesNeeded = (totalBits + 7) / 8;
        var sb = new StringBuilder(targetChars);

        var buffer = 0UL;
        var bitsInBuffer = 0;
        var charsEmitted = 0;
        var byteIndex = 0;

        while (charsEmitted < targetChars)
        {
            while (bitsInBuffer < bitsPerChar && byteIndex < bytesNeeded)
            {
                buffer = (buffer << 8) | bytes[byteIndex];
                bitsInBuffer += 8;
                byteIndex++;
            }
            var shift = bitsInBuffer - bitsPerChar;
            var index = (int)((buffer >> shift) & 0x1F);
            sb.Append(Base32Alphabet[index]);
            buffer &= (1UL << shift) - 1;
            bitsInBuffer = shift;
            charsEmitted++;
        }

        return sb.ToString();
    }

    private static string RequireLeaf(string? value, string argName, string humanName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{humanName} is required for the chosen entity type.", argName);
        }
        return value;
    }
}
