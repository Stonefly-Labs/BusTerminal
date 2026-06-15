using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Inventory;

// Spec 008 / T101. Filter + sort + paging criteria for the inventory list.
// Environment is OPTIONAL (in contrast to spec 006's env-scoped browse) —
// US2 explicitly supports cross-env discovery for on-call engineers searching
// by partial name or business unit.
public sealed record NamespaceInventoryQuery(
    string? Environment = null,
    IReadOnlyList<LifecycleStatus>? LifecycleStatuses = null,
    IReadOnlyList<ValidationStatus>? ValidationStatuses = null,
    string? TagKey = null,
    string? TagValue = null,
    string? Search = null,
    NamespaceInventorySort Sort = NamespaceInventorySort.LastValidatedAtDesc,
    bool IncludeArchived = false,
    int PageSize = 25,
    string? ContinuationToken = null);

public enum NamespaceInventorySort
{
    LastValidatedAtDesc,
    LastValidatedAtAsc,
    DisplayNameAsc,
    DisplayNameDesc,
    EnvironmentAsc,
    EnvironmentDesc,
}
