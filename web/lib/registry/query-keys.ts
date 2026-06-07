/**
 * Spec 006 / T056. TanStack Query key factories for the registry slice.
 *
 * Stable, hierarchical keys give predictable invalidation patterns:
 *   - `registry.entities.list()` invalidates every list
 *   - `registry.entities.list({ environment: "dev" })` invalidates just dev lists
 *   - `registry.entities.detail(id)` is point-precise
 *
 * Convention follows TanStack Query 5 best practices (per research §14):
 *   - First element is the slice tag `"registry"` so cross-slice
 *     invalidations stay tractable.
 *   - Second element is the resource family (`entities`, `audit`, `search`).
 *   - Optional filter object lives last; consumers pass an empty object to
 *     match every entry in that family.
 */

import type { RegistryEntityType } from "./schemas";

export interface RegistryListFilter {
  readonly environment?: string;
  readonly entityType?: RegistryEntityType;
  readonly parentId?: string;
  readonly status?: string;
}

export const registryQueryKeys = {
  all: ["registry"] as const,

  entities: {
    all: ["registry", "entities"] as const,
    list: (filter: RegistryListFilter = {}) => ["registry", "entities", "list", filter] as const,
    detail: (id: string) => ["registry", "entities", "detail", id] as const,
  },

  audit: {
    all: ["registry", "audit"] as const,
    forEntity: (entityId: string, limit: number = 50) =>
      ["registry", "audit", entityId, { limit }] as const,
  },

  search: {
    all: ["registry", "search"] as const,
    query: (query: string, filters: Record<string, unknown> = {}) =>
      ["registry", "search", query, filters] as const,
  },

  environments: {
    all: ["registry", "environments"] as const,
    list: () => ["registry", "environments", "list"] as const,
  },
} as const;
