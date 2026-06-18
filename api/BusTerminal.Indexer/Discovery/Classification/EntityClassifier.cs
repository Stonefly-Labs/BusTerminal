namespace BusTerminal.Indexer.Discovery.Classification;

// Spec 009 / T051 + R-08 + FR-013.
//   - New      → no prior document, or prior `azureSourcedHash` is null.
//   - Unchanged→ hash matches prior; only `lastSeenUtc` should be touched.
//   - Updated  → hash differs from prior; azureSourced.* must be overwritten,
//                curated metadata + serviceAssociations stay untouched.
//   - Missing → entity was Active before, run did not observe it. Handled by
//               the missing-sweep pass after the streaming walk completes.
public static class EntityClassifier
{
    public static ClassificationOutcome Classify(string? priorHash, string currentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentHash);

        if (string.IsNullOrEmpty(priorHash))
        {
            return ClassificationOutcome.New;
        }
        return string.Equals(priorHash, currentHash, StringComparison.Ordinal)
            ? ClassificationOutcome.Unchanged
            : ClassificationOutcome.Updated;
    }
}

public enum ClassificationOutcome
{
    New,
    Updated,
    Unchanged,
    Missing,
}
