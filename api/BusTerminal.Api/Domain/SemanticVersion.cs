using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-011. Matches version-info.schema.json.
[JsonConverter(typeof(JsonStringEnumConverter<CompatibilityIndicator>))]
public enum CompatibilityIndicator
{
    Backward,
    Forward,
    Full,
    None,
}

public sealed record SemanticVersionRef(int Major, int Minor, int Patch) : IComparable<SemanticVersionRef>
{
    public int CompareTo(SemanticVersionRef? other)
    {
        if (other is null)
        {
            return 1;
        }

        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0)
        {
            return majorCompare;
        }

        var minorCompare = Minor.CompareTo(other.Minor);
        return minorCompare != 0 ? minorCompare : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record HistoricalVersionEntry(
    int Major,
    int Minor,
    int Patch,
    LifecycleState Lifecycle,
    DateTimeOffset? DeprecatedAt = null,
    SemanticVersionRef? ReplacedBy = null);

public sealed record SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    CompatibilityIndicator? Compatibility = null,
    SemanticVersionRef? CurrentVersionRef = null,
    IReadOnlyCollection<HistoricalVersionEntry>? VersionHistory = null) : IComparable<SemanticVersion>
{
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0)
        {
            return majorCompare;
        }

        var minorCompare = Minor.CompareTo(other.Minor);
        return minorCompare != 0 ? minorCompare : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
