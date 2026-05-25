using System.Collections.Concurrent;

namespace BusTerminal.Api.Domain;

// Spec 004 / Q4 / FR-002. Hand-maintained, mutable-at-startup registry mapping
// discriminator strings to CLR types. Per tech-stack.md §1 ("minimize hidden magic;
// prefer explicitness over convention-heavy frameworks"), entries are registered
// explicitly by the per-type modules in US1 (T072) rather than discovered by
// reflection.
//
// Thread-safety: registrations land at startup (DI composition root); reads happen
// from many threads at deserialization time. Use a concurrent dictionary so reads
// never block writes if a late slice registers an additional type.
public sealed class ResourceTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _byDiscriminator;
    private readonly ConcurrentDictionary<Type, string> _byClrType;

    public ResourceTypeRegistry()
    {
        _byDiscriminator = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        _byClrType = new ConcurrentDictionary<Type, string>();
    }

    public void Register(string discriminator, Type clrType)
    {
        ArgumentException.ThrowIfNullOrEmpty(discriminator);
        ArgumentNullException.ThrowIfNull(clrType);

        if (!typeof(Resource).IsAssignableFrom(clrType))
        {
            throw new ArgumentException(
                $"Type {clrType.FullName} must derive from Resource to register as a discriminator.",
                nameof(clrType));
        }

        _byDiscriminator[discriminator] = clrType;
        _byClrType[clrType] = discriminator;
    }

    // Symmetric removal — used by the additive-evolution guard test (T158 / SC-010)
    // to simulate a future build's type vanishing from the current build's registry
    // (a downgrade or sibling deployment scenario). Returns true if an entry was
    // removed. Production code does not call this — entries are registered once at
    // composition time.
    public bool Unregister(string discriminator)
    {
        ArgumentException.ThrowIfNullOrEmpty(discriminator);

        if (_byDiscriminator.TryRemove(discriminator, out var clrType))
        {
            _byClrType.TryRemove(clrType, out _);
            return true;
        }

        return false;
    }

    public bool TryGetType(string discriminator, out Type clrType)
    {
        if (_byDiscriminator.TryGetValue(discriminator, out var found))
        {
            clrType = found;
            return true;
        }

        clrType = typeof(object);
        return false;
    }

    public string GetDiscriminator(Type clrType)
    {
        if (_byClrType.TryGetValue(clrType, out var discriminator))
        {
            return discriminator;
        }

        throw new InvalidOperationException(
            $"Type {clrType.FullName} is not registered. Register it during DI composition before serializing.");
    }

    public bool IsKnown(string discriminator) => _byDiscriminator.ContainsKey(discriminator);

    public IReadOnlyCollection<string> KnownDiscriminators => _byDiscriminator.Keys.ToList();
}
