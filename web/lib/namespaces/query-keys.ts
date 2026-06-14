/**
 * Spec 008 / T051. TanStack Query key factories for the namespace slice.
 *
 * Stable, hierarchical keys give predictable invalidation patterns:
 *   - `namespaceKeys.all` invalidates every namespace query
 *   - `namespaceKeys.inventory.list()` invalidates every inventory list
 *   - `namespaceKeys.details(id)` is point-precise
 *   - `namespaceKeys.validationRuns.list(id)` covers the runs for one namespace
 *
 * Convention mirrors `web/lib/registry/query-keys.ts` (spec 006).
 */

export interface NamespaceInventoryFilter {
  readonly environment?: string;
  readonly lifecycleStatus?: string;
  readonly validationStatus?: string;
  readonly includeArchived?: boolean;
  readonly q?: string;
  readonly tagKey?: string;
  readonly tagValue?: string;
}

export const namespaceKeys = {
  all: ["namespaces"] as const,

  identity: () => ["namespaces", "identity"] as const,

  picker: {
    all: ["namespaces", "picker"] as const,
    search: (query: string, includeGroups: boolean = true) =>
      ["namespaces", "picker", query, { includeGroups }] as const,
  },

  inventory: {
    all: ["namespaces", "inventory"] as const,
    list: (filter: NamespaceInventoryFilter = {}) =>
      ["namespaces", "inventory", "list", filter] as const,
  },

  details: (id: string) => ["namespaces", "detail", id] as const,

  validationRuns: {
    all: (namespaceId: string) =>
      ["namespaces", "validation-runs", namespaceId] as const,
    list: (namespaceId: string, limit: number = 25) =>
      ["namespaces", "validation-runs", namespaceId, "list", { limit }] as const,
    detail: (namespaceId: string, runId: string) =>
      ["namespaces", "validation-runs", namespaceId, "detail", runId] as const,
  },
} as const;
