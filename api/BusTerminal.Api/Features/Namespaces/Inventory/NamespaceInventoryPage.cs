using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Features.Namespaces.Inventory;

// Spec 008 / T101 / US2. Returned shape of IRegistryEntityStore.ListOnboardedAsync.
public sealed record NamespaceInventoryPage(
    IReadOnlyList<RegistryNamespace> Items,
    string? ContinuationToken);
