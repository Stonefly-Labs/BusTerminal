/**
 * Spec 006 / T052. TypeScript types inferred from the Zod schemas in
 * `./schemas.ts`. Re-exported here so consumers can import the types
 * without pulling Zod into RSC bundles unnecessarily.
 *
 * Adding a type? Add the schema in `./schemas.ts` first, then re-export
 * the inferred type here — the shared-schema contract test (T060) keeps
 * the Zod schemas honest against the canonical JSON schemas.
 */

export type {
  AuditActor,
  AuditEvent,
  AuditEventType,
  AuditFieldChange,
  ConflictChangedField,
  ConflictResponse,
  HasChildrenResponse,
  RegistryEntity,
  RegistryEntityCreateRequest,
  RegistryEntityPage,
  RegistryEntityStatus,
  RegistryEntityType,
  RegistryEntityUpdateRequest,
  RegistrySearchHit,
  RegistrySearchResponse,
  RegistrySource,
  RegistryTag,
} from "./schemas";

export { REGISTRY_ENTITY_TYPES } from "./schemas";
