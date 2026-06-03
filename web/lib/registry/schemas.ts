/**
 * Spec 006 / T052. Zod schemas mirroring
 * `specs/006-service-bus-registry-core/contracts/registry-entity.schema.json`,
 * `conflict-response.schema.json`, and `audit-event.schema.json`.
 *
 * These are the single source of truth for client-side validation. The
 * backend FluentValidation rules in
 * `api/BusTerminal.Api/Features/Registry/_Shared/RegistryEntityValidationRules.cs`
 * mirror these constraints; the shared-schema contract test (T060/T061)
 * walks both sides against the canonical JSON Schemas.
 */

import { z } from "zod";

export const REGISTRY_ENTITY_TYPES = [
  "Namespace",
  "Queue",
  "Topic",
  "Subscription",
  "Rule",
] as const;
export const registryEntityTypeSchema = z.enum(REGISTRY_ENTITY_TYPES);
export type RegistryEntityType = z.infer<typeof registryEntityTypeSchema>;

export const registryEntityStatusSchema = z.enum(["Active", "Deprecated"]);
export type RegistryEntityStatus = z.infer<typeof registryEntityStatusSchema>;

export const registrySourceSchema = z.enum(["Manual"]);
export type RegistrySource = z.infer<typeof registrySourceSchema>;

// Base name pattern (FR-015 / data-model.md §3.1). Per-entity-type
// specialization (Namespace 6–50 chars, etc.) is layered on by the per-type
// form validators.
const baseNamePattern = /^[A-Za-z0-9][A-Za-z0-9._\-/]{0,259}$/;

export const registryTagSchema = z.object({
  key: z.string().min(1).max(256),
  value: z.string().min(1).max(1024),
});
export type RegistryTag = z.infer<typeof registryTagSchema>;

// JSON Schema describes `metadata` as "extensible structured metadata";
// model it as an unknown record so callers don't need to declare every
// possible shape.
export const registryMetadataSchema = z.record(z.string(), z.unknown()).nullable().optional();

export const registryEntitySchema = z.object({
  id: z.string().uuid(),
  entityType: registryEntityTypeSchema,
  name: z
    .string()
    .min(1)
    .max(260)
    .regex(baseNamePattern, "Name does not match the base Azure Service Bus naming pattern."),
  fullyQualifiedName: z.string().nullable().optional(),
  description: z.string().max(4000).nullable().optional(),
  tags: z.array(registryTagSchema).max(50).default([]),
  owner: z.string().max(512).nullable().optional(),
  environment: z.string().min(1).max(64),
  status: registryEntityStatusSchema,
  createdAtUtc: z.string().datetime(),
  updatedAtUtc: z.string().datetime(),
  source: registrySourceSchema,
  azureResourceId: z.string().max(2048).nullable().optional(),
  namespaceName: z.string().max(260).nullable().optional(),
  metadata: registryMetadataSchema,
  parentId: z.string().uuid().nullable().optional(),
});

export type RegistryEntity = z.infer<typeof registryEntitySchema>;

// Request schemas — what the client sends. The server stamps `id` /
// `createdAtUtc` / `updatedAtUtc` / `source` / `fullyQualifiedName` so they
// are stripped from the create-request shape.
export const registryEntityCreateRequestSchema = registryEntitySchema
  .partial({
    fullyQualifiedName: true,
    createdAtUtc: true,
    updatedAtUtc: true,
  })
  .omit({
    fullyQualifiedName: true,
    createdAtUtc: true,
    updatedAtUtc: true,
  });
export type RegistryEntityCreateRequest = z.infer<typeof registryEntityCreateRequestSchema>;

// Update request — includes the `_overwriteAcknowledged` extension consumed
// by UpdateEndpoint (research §8 / T078).
export const registryEntityUpdateRequestSchema = registryEntityCreateRequestSchema.extend({
  _overwriteAcknowledged: z.boolean().optional(),
});
export type RegistryEntityUpdateRequest = z.infer<typeof registryEntityUpdateRequestSchema>;

// Conflict response (FR-020 / contracts/conflict-response.schema.json).
export const conflictChangedFieldSchema = z.object({
  field: z.string(),
  currentValue: z.unknown(),
  submittedValue: z.unknown(),
});
export type ConflictChangedField = z.infer<typeof conflictChangedFieldSchema>;

export const conflictResponseSchema = z.object({
  type: z.literal("https://busterminal.dev/probs/concurrency-conflict"),
  title: z.literal("Concurrency conflict"),
  status: z.literal(409),
  code: z.literal("ConcurrencyConflict"),
  detail: z.string().optional(),
  instance: z.string().optional(),
  entityId: z.string().uuid(),
  currentVersion: z.string(),
  submittedVersion: z.string(),
  currentEntity: registryEntitySchema,
  changedFields: z.array(conflictChangedFieldSchema),
});
export type ConflictResponse = z.infer<typeof conflictResponseSchema>;

// HasChildren response (FR-009 / contracts/registry-api.yaml#HasChildrenResponse).
export const hasChildrenResponseSchema = z.object({
  type: z.literal("https://busterminal.dev/probs/has-children"),
  title: z.string(),
  status: z.literal(409),
  code: z.literal("HasChildren"),
  detail: z.string().optional(),
  instance: z.string().optional(),
  entityId: z.string().uuid(),
  totalChildren: z.number().int().nonnegative(),
  childrenByType: z.record(registryEntityTypeSchema, z.number().int().nonnegative()),
});
export type HasChildrenResponse = z.infer<typeof hasChildrenResponseSchema>;

// Audit event (FR-032 / contracts/audit-event.schema.json).
export const auditEventTypeSchema = z.enum([
  "Created",
  "Updated",
  "Deleted",
  "StatusChanged",
]);
export type AuditEventType = z.infer<typeof auditEventTypeSchema>;

export const auditActorSchema = z.object({
  principalId: z.string(),
  displayName: z.string(),
});
export type AuditActor = z.infer<typeof auditActorSchema>;

export const auditFieldChangeSchema = z.object({
  field: z.string(),
  before: z.unknown().optional(),
  after: z.unknown().optional(),
});
export type AuditFieldChange = z.infer<typeof auditFieldChangeSchema>;

export const auditEventSchema = z.object({
  id: z.string().uuid(),
  entityId: z.string().uuid(),
  entityType: registryEntityTypeSchema,
  environment: z.string().min(1),
  eventType: auditEventTypeSchema,
  timestamp: z.string().datetime(),
  actor: auditActorSchema,
  changeSummary: z.string().max(1000),
  fieldChanges: z.array(auditFieldChangeSchema).nullable().optional(),
  wasForceOverwrite: z.boolean(),
  correlationId: z.string(),
});
export type AuditEvent = z.infer<typeof auditEventSchema>;

// Search response (contracts/registry-api.yaml#SearchResponse).
export const registrySearchHitSchema = z.object({
  id: z.string().uuid(),
  entityType: registryEntityTypeSchema,
  name: z.string(),
  fullyQualifiedName: z.string().nullable().optional(),
  environment: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  owner: z.string().nullable().optional(),
  namespaceName: z.string().nullable().optional(),
  parentId: z.string().uuid().nullable().optional(),
  score: z.number().nullable().optional(),
});
export type RegistrySearchHit = z.infer<typeof registrySearchHitSchema>;

export const registrySearchResponseSchema = z.object({
  hits: z.array(registrySearchHitSchema),
  totalCount: z.number().int().nullable().optional(),
});
export type RegistrySearchResponse = z.infer<typeof registrySearchResponseSchema>;

// List response (paginated).
export const registryEntityPageSchema = z.object({
  items: z.array(registryEntitySchema),
  continuationToken: z.string().nullable().optional(),
});
export type RegistryEntityPage = z.infer<typeof registryEntityPageSchema>;
