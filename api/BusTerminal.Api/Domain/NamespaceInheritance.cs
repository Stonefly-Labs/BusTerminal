using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Persistence;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-003 + Edge Case "Ownership of a Namespace itself."
// Resolves inherited governance / ownership metadata by walking the parent chain
// of a NamespacePath. Override semantics: a child namespace's explicit metadata
// wins over inherited; only missing fields are filled from the chain.
public sealed class NamespaceInheritance
{
    private readonly ICanonicalResourceStore _store;

    public NamespaceInheritance(ICanonicalResourceStore store)
    {
        _store = store;
    }

    // Returns the inherited shape walking root → leaf. Caller composes the
    // child's own values on top, taking precedence over inherited values.
    public async Task<InheritedNamespaceMetadata> ResolveAsync(
        NamespacePath path,
        CancellationToken cancellationToken)
    {
        var chain = new List<Namespace>();
        var current = (NamespacePath?)path;
        while (current is { } cur)
        {
            var match = await FindByPathAsync(cur, cancellationToken).ConfigureAwait(false);
            if (match is not null)
            {
                chain.Add(match);
            }

            current = cur.Parent;
        }

        // Walk root → leaf so closer ancestors overwrite farther ancestors but
        // never the child's own values (which the caller layers on top).
        chain.Reverse();

        OwnershipRecord? ownership = null;
        ClassificationMetadata? classification = null;
        var tags = new List<TagReference>();

        foreach (var ns in chain)
        {
            ownership = ns.Ownership ?? ownership;
            classification = ns.Classification ?? classification;
            foreach (var tag in ns.Tags)
            {
                if (!tags.Any(t => t.TagId == tag.TagId))
                {
                    tags.Add(tag);
                }
            }
        }

        return new InheritedNamespaceMetadata(ownership, classification, tags);
    }

    private async Task<Namespace?> FindByPathAsync(NamespacePath path, CancellationToken cancellationToken)
    {
        await foreach (var resource in _store.QueryAsync(
            new ResourceQuery.ByNamespacePath(path),
            cancellationToken).ConfigureAwait(false))
        {
            if (resource is Namespace ns)
            {
                return ns;
            }
        }

        return null;
    }
}

public sealed record InheritedNamespaceMetadata(
    OwnershipRecord? InheritedOwnership,
    ClassificationMetadata? InheritedClassification,
    IReadOnlyCollection<TagReference> InheritedTags);
