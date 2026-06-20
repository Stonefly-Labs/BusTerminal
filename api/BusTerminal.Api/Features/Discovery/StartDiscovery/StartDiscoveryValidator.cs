using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using FluentValidation;

namespace BusTerminal.Api.Features.Discovery.StartDiscovery;

// Spec 009 / T044. Validation context for the start-discovery endpoint.
// The request body itself is empty (route + principal carry all the state),
// so this validator instead exists as the wire-up for any future body-level
// checks; the endpoint handler does the namespace-exists + lifecycle gate
// inline against IRegistryEntityStore because those checks need DB access.
public sealed class StartDiscoveryValidator : AbstractValidator<StartDiscoveryRequest>
{
    public StartDiscoveryValidator()
    {
        // No body — kept as an extension point. Removing it would break the
        // contract test that asserts a validator is registered for the request
        // type (mirrors the Spec 008 onboarding validator pattern).
        RuleFor(_ => 0).Must(_ => true);
    }
}

// Spec 009 / T045. Helper that the StartDiscovery endpoint uses to check
// the namespace exists and is in a discoverable lifecycle state. Returns a
// failure reason for the handler to translate to 404 / 409 — the discovery
// surface deliberately mirrors the spec-008 lifecycle gating so admins see
// a consistent error story.
public interface IStartDiscoveryNamespaceGate
{
    Task<NamespaceDiscoveryGateOutcome> CheckAsync(Guid namespaceId, CancellationToken cancellationToken);
}

public enum NamespaceDiscoveryGate
{
    Allowed,
    NamespaceNotFound,
    LifecycleBlocked,
}

public sealed record NamespaceDiscoveryGateOutcome(NamespaceDiscoveryGate Outcome, string? Reason);

public sealed class StartDiscoveryNamespaceGate : IStartDiscoveryNamespaceGate
{
    private readonly IRegistryEntityStore _entityStore;

    public StartDiscoveryNamespaceGate(IRegistryEntityStore entityStore)
    {
        _entityStore = entityStore;
    }

    public async Task<NamespaceDiscoveryGateOutcome> CheckAsync(Guid namespaceId, CancellationToken cancellationToken)
    {
        // Cross-environment lookup — registered namespaces carry their own
        // environment; the discovery endpoint doesn't need to thread that in
        // because the namespace id is globally unique within the platform.
        var entity = await _entityStore.FindByIdAsync(namespaceId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return new NamespaceDiscoveryGateOutcome(NamespaceDiscoveryGate.NamespaceNotFound,
                $"Namespace {namespaceId:D} is not registered.");
        }
        if (entity is not RegistryNamespace ns)
        {
            return new NamespaceDiscoveryGateOutcome(NamespaceDiscoveryGate.NamespaceNotFound,
                $"Resource {namespaceId:D} is not a namespace.");
        }
        // Spec 008 lifecycle gates — only Active namespaces can be discovered;
        // Deprecated namespaces should not poll Azure (cost + noise). A null
        // lifecycle is treated as Active (legacy registry namespaces).
        var lifecycle = ns.LifecycleStatus ?? LifecycleStatus.Active;
        if (lifecycle is not LifecycleStatus.Active)
        {
            return new NamespaceDiscoveryGateOutcome(NamespaceDiscoveryGate.LifecycleBlocked,
                $"Namespace lifecycleStatus is {lifecycle}; only Active namespaces support discovery.");
        }
        return new NamespaceDiscoveryGateOutcome(NamespaceDiscoveryGate.Allowed, Reason: null);
    }
}
