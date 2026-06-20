/**
 * Spec 009 / T027. Zod schemas mirroring
 * `specs/009-entity-discovery-publication/contracts/openapi.yaml`.
 *
 * Single source of truth for client-side validation of the discovery and
 * published-entity surfaces. Every response from `web/lib/discovery/api.ts`
 * is parsed through these before being returned to callers.
 */

import { z } from "zod";

export const ENTITY_TYPES = ["Queue", "Topic", "Subscription", "Rule"] as const;
export const entityTypeSchema = z.enum(ENTITY_TYPES);
export type EntityType = z.infer<typeof entityTypeSchema>;

export const LIFECYCLE_STATUSES = ["Active", "Missing", "Archived"] as const;
export const lifecycleStatusSchema = z.enum(LIFECYCLE_STATUSES);
export type LifecycleStatus = z.infer<typeof lifecycleStatusSchema>;

export const ENTITY_SERVICE_ROLES = ["Owner", "Producer", "Consumer"] as const;
export const entityServiceRoleSchema = z.enum(ENTITY_SERVICE_ROLES);
export type EntityServiceRole = z.infer<typeof entityServiceRoleSchema>;

export const DISCOVERY_RUN_STATUSES = ["Queued", "InProgress", "Succeeded", "Failed"] as const;
export const discoveryRunStatusSchema = z.enum(DISCOVERY_RUN_STATUSES);
export type DiscoveryRunStatus = z.infer<typeof discoveryRunStatusSchema>;

export const DISCOVERY_FAILURE_CATEGORIES = [
  "Authn",
  "Authz",
  "NotFound",
  "Throttled",
  "Transport",
  "Internal",
  "WorkerLost",
  "Unknown",
] as const;
export const discoveryFailureCategorySchema = z.enum(DISCOVERY_FAILURE_CATEGORIES);
export type DiscoveryFailureCategory = z.infer<typeof discoveryFailureCategorySchema>;

export const DISCOVERY_PHASES = [
  "LockAcquire",
  "FetchQueues",
  "FetchTopics",
  "FetchSubscriptions",
  "FetchRules",
  "Persist",
  "ResultWrite",
] as const;
export const discoveryPhaseSchema = z.enum(DISCOVERY_PHASES);
export type DiscoveryPhase = z.infer<typeof discoveryPhaseSchema>;

export const entityServiceAssociationSchema = z.object({
  associationId: z.string(),
  serviceId: z.string(),
  role: entityServiceRoleSchema,
  createdUtc: z.string().datetime({ offset: true }),
  createdBy: z.string(),
});
export type EntityServiceAssociation = z.infer<typeof entityServiceAssociationSchema>;

const discoveryFailureSchema = z.object({
  category: discoveryFailureCategorySchema,
  message: z.string(),
  occurredAtPhase: discoveryPhaseSchema,
  retriesExhausted: z.number().int().nullable().optional(),
});

const coalescedRequestSchema = z.object({
  requestedUtc: z.string().datetime({ offset: true }).optional(),
  requestedBy: z.string().optional(),
});

export const discoveryRunSchema = z.object({
  id: z.string(),
  schemaVersion: z.string(),
  namespaceId: z.string(),
  status: discoveryRunStatusSchema,
  trigger: z.literal("Manual"),
  startedUtc: z.string().datetime({ offset: true }),
  completedUtc: z.string().datetime({ offset: true }).nullable().optional(),
  durationMs: z.number().int().nullable().optional(),
  requestedBy: z.string(),
  queueCount: z.number().int().optional(),
  topicCount: z.number().int().optional(),
  subscriptionCount: z.number().int().optional(),
  ruleCount: z.number().int().optional(),
  newCount: z.number().int().optional(),
  updatedCount: z.number().int().optional(),
  unchangedCount: z.number().int().optional(),
  missingCount: z.number().int().optional(),
  failure: discoveryFailureSchema.nullable().optional(),
  coalescedRequests: z.array(coalescedRequestSchema).optional(),
  correlationId: z.string().optional(),
});
export type DiscoveryRun = z.infer<typeof discoveryRunSchema>;

export const publishedEntitySummarySchema = z.object({
  id: z.string(),
  entityType: entityTypeSchema,
  namespaceId: z.string(),
  name: z.string(),
  parentEntityId: z.string().nullable().optional(),
  lifecycleStatus: lifecycleStatusSchema,
  lastSeenUtc: z.string().datetime({ offset: true }).optional(),
  associatedServiceIds: z.array(z.string()).optional(),
  associationRoles: z.array(entityServiceRoleSchema).optional(),
  tags: z.array(z.string()).optional(),
});
export type PublishedEntitySummary = z.infer<typeof publishedEntitySummarySchema>;

const documentationLinkSchema = z.object({
  label: z.string(),
  url: z.string().url(),
});

const contactInformationSchema = z
  .object({
    primaryContact: z.string().optional(),
    escalationPath: z.string().optional(),
  })
  .nullable();

export const publishedEntitySchema = publishedEntitySummarySchema.extend({
  schemaVersion: z.string(),
  environment: z.string(),
  compositeKey: z.string(),
  displayName: z.string().optional(),
  description: z.string().nullable().optional(),
  businessPurpose: z.string().nullable().optional(),
  documentationLinks: z.array(documentationLinkSchema).optional(),
  contactInformation: contactInformationSchema.optional(),
  operationalNotes: z.string().nullable().optional(),
  lifecycleStatusChangedUtc: z.string().datetime({ offset: true }).optional(),
  firstDiscoveredUtc: z.string().datetime({ offset: true }),
  lastDiscoveryRunId: z.string().optional(),
  azureSourced: z.record(z.string(), z.unknown()),
  azureSourcedHash: z.string(),
  serviceAssociations: z.array(entityServiceAssociationSchema),
  etag: z.string().optional(),
});
export type PublishedEntity = z.infer<typeof publishedEntitySchema>;

export const startDiscoveryResponseSchema = z.object({
  discoveryRunId: z.string(),
  namespaceId: z.string().optional(),
  status: discoveryRunStatusSchema,
  coalescedFromExisting: z.boolean(),
  startedUtc: z.string().datetime({ offset: true }).optional(),
});
export type StartDiscoveryResponse = z.infer<typeof startDiscoveryResponseSchema>;

export const updateEntityMetadataRequestSchema = z.object({
  description: z.string().nullable().optional(),
  businessPurpose: z.string().nullable().optional(),
  tags: z.array(z.string()).optional(),
  documentationLinks: z.array(documentationLinkSchema).optional(),
  contactInformation: contactInformationSchema.optional(),
  operationalNotes: z.string().nullable().optional(),
});
export type UpdateEntityMetadataRequest = z.infer<typeof updateEntityMetadataRequestSchema>;

export const addAssociationRequestSchema = z.object({
  serviceId: z.string(),
  role: entityServiceRoleSchema,
});
export type AddAssociationRequest = z.infer<typeof addAssociationRequestSchema>;

export const discoveryRunPageSchema = z.object({
  items: z.array(discoveryRunSchema),
  continuationToken: z.string().nullable().optional(),
});
export type DiscoveryRunPage = z.infer<typeof discoveryRunPageSchema>;

export const publishedEntitySearchResponseSchema = z.object({
  items: z.array(publishedEntitySummarySchema),
  totalCount: z.number().int(),
  page: z.number().int(),
  pageSize: z.number().int(),
});
export type PublishedEntitySearchResponse = z.infer<typeof publishedEntitySearchResponseSchema>;
