/**
 * Spec 008 / T048. Zod schemas mirroring the backend FluentValidation rules
 * and `specs/008-namespace-onboarding/contracts/*.schema.json`.
 *
 * Authoring source-of-truth flow:
 *   contracts/*.schema.json → backend FluentValidation rules → these Zod schemas.
 * A contract test (added in Phase 6) keeps both halves honest against the
 * canonical JSON Schemas.
 */

import { z } from "zod";

// === Enums ===

export const lifecycleStatusSchema = z.enum(["Active", "Disabled", "Archived"]);
export type LifecycleStatus = z.infer<typeof lifecycleStatusSchema>;

export const validationStatusSchema = z.enum(["Healthy", "Degraded", "Unhealthy"]);
export type ValidationStatus = z.infer<typeof validationStatusSchema>;

export const validationCheckNameSchema = z.enum([
  "Existence",
  "Accessibility",
  "RequiredPermissions",
  "IdentityAuthorization",
  "ApiReachability",
]);
export type ValidationCheckName = z.infer<typeof validationCheckNameSchema>;

export const validationCheckOutcomeSchema = z.enum(["Pass", "Fail", "Skipped"]);
export type ValidationCheckOutcome = z.infer<typeof validationCheckOutcomeSchema>;

export const validationFailureCategorySchema = z.enum([
  "Ok",
  "Timeout",
  "Unauthorized",
  "NotFound",
  "Throttled",
  "CrossTenant",
  "Unknown",
]);
export type ValidationFailureCategory = z.infer<typeof validationFailureCategorySchema>;

export const ownershipRoleSchema = z.enum([
  "PrimaryOwner",
  "SecondaryOwner",
  "TechnicalSteward",
  "SupportContact",
]);
export type OwnershipRole = z.infer<typeof ownershipRoleSchema>;

export const principalTypeSchema = z.enum(["User", "Group"]);
export type PrincipalType = z.infer<typeof principalTypeSchema>;

export const lifecycleActionSchema = z.enum(["disable", "enable", "archive", "restore"]);
export type LifecycleAction = z.infer<typeof lifecycleActionSchema>;

// === Records ===

export const ownershipAssignmentSchema = z.object({
  role: ownershipRoleSchema,
  principalType: principalTypeSchema,
  objectId: z.string().uuid(),
  displayNameSnapshot: z.string().min(1).max(256),
  assignedAtUtc: z.string().datetime({ offset: true }),
  assignedBy: z.string().uuid(),
});
export type OwnershipAssignment = z.infer<typeof ownershipAssignmentSchema>;

export const ownershipBlockSchema = z.object({
  primaryOwner: ownershipAssignmentSchema,
  secondaryOwners: z.array(ownershipAssignmentSchema).default([]),
  technicalStewards: z.array(ownershipAssignmentSchema).default([]),
  supportContacts: z.array(ownershipAssignmentSchema).default([]),
});
export type OwnershipBlock = z.infer<typeof ownershipBlockSchema>;

export const onboardingActorSchema = z.object({
  objectId: z.string().uuid(),
  displayNameSnapshot: z.string().max(256),
  onboardedAtUtc: z.string().datetime({ offset: true }),
});
export type OnboardingActor = z.infer<typeof onboardingActorSchema>;

// Canonical ARM Service Bus namespace identifier — pattern mirrors
// contracts/onboarded-namespace.schema.json.
export const armResourceIdSchema = z
  .string()
  .regex(
    /^\/subscriptions\/[0-9a-fA-F-]{36}\/resourceGroups\/[^/]{1,90}\/providers\/Microsoft\.ServiceBus\/namespaces\/[A-Za-z][A-Za-z0-9-]{4,48}[A-Za-z0-9]$/,
    "Azure Resource ID does not match the canonical Service Bus namespace pattern.",
  );

export const registryTagSchema = z.object({
  key: z.string().min(1).max(256),
  value: z.string().min(1).max(1024),
});
export type RegistryTag = z.infer<typeof registryTagSchema>;

// === Validation run ===

export const validationCheckResultSchema = z.object({
  name: validationCheckNameSchema,
  outcome: validationCheckOutcomeSchema,
  reason: z.string().min(1).max(256),
  reasonCategory: validationFailureCategorySchema,
  durationMs: z.number().int().min(0),
  correlationRequestId: z.string().nullable().optional(),
});
export type ValidationCheckResult = z.infer<typeof validationCheckResultSchema>;

export const armResourceSnapshotSchema = z.object({
  region: z.string(),
  resourceGroup: z.string(),
  subscriptionId: z.string().uuid(),
  capturedAtUtc: z.string().datetime({ offset: true }),
});
export type ArmResourceSnapshot = z.infer<typeof armResourceSnapshotSchema>;

export const driftFieldSchema = z.object({
  field: z.enum(["region", "resourceGroup", "subscriptionId"]),
  persistedValue: z.string(),
  observedValue: z.string(),
});
export type DriftField = z.infer<typeof driftFieldSchema>;

export const validationRunSchema = z.object({
  id: z.string().uuid(),
  namespaceId: z.string().uuid(),
  executedAtUtc: z.string().datetime({ offset: true }),
  executedBy: z.string().uuid(),
  executedByDisplayNameSnapshot: z.string().max(256),
  azureResourceIdAtRun: z.string(),
  aggregateStatus: validationStatusSchema,
  checkResults: z.array(validationCheckResultSchema).length(5),
  armResourceSnapshot: armResourceSnapshotSchema.nullable().optional(),
  driftDetected: z.boolean(),
  driftFields: z.array(driftFieldSchema).default([]),
  totalDurationMs: z.number().int().min(0),
});
export type ValidationRun = z.infer<typeof validationRunSchema>;

// === Namespace document ===

export const onboardedNamespaceSchema = z.object({
  id: z.string().uuid(),
  entityType: z.literal("Namespace"),
  source: z.enum(["Manual", "Onboarded"]),
  name: z.string().min(6).max(50),
  fullyQualifiedName: z.string().nullable().optional(),
  displayName: z.string().min(1).max(200).nullable().optional(),
  description: z.string().max(4000).nullable().optional(),
  environment: z.string().min(1).max(100),
  status: z.enum(["Active", "Deprecated"]),
  azureResourceId: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().nullable().optional(),
  subscriptionName: z.string().max(256).nullable().optional(),
  resourceGroup: z.string().max(90).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  region: z.string().max(64).nullable().optional(),
  businessUnit: z.string().max(200).nullable().optional(),
  productOrApplication: z.string().max(200).nullable().optional(),
  costCenter: z.string().max(100).nullable().optional(),
  notes: z.string().max(4000).nullable().optional(),
  tags: z.array(registryTagSchema).max(50).default([]),
  lifecycleStatus: lifecycleStatusSchema.nullable().optional(),
  validationStatus: validationStatusSchema.nullable().optional(),
  lastValidationRunId: z.string().uuid().nullable().optional(),
  lastValidatedAtUtc: z.string().datetime({ offset: true }).nullable().optional(),
  ownership: ownershipBlockSchema.nullable().optional(),
  onboardingActor: onboardingActorSchema.nullable().optional(),
  createdAtUtc: z.string().datetime({ offset: true }),
  updatedAtUtc: z.string().datetime({ offset: true }),
});
export type OnboardedNamespace = z.infer<typeof onboardedNamespaceSchema>;

// === Request DTOs ===

export const onboardingRequestSchema = z.object({
  id: z.string().uuid(),
  azureResourceId: armResourceIdSchema,
  displayName: z.string().min(1).max(200),
  environment: z.string().min(1).max(100),
  description: z.string().max(4000).nullable().optional(),
  businessUnit: z.string().max(200).nullable().optional(),
  productOrApplication: z.string().max(200).nullable().optional(),
  costCenter: z.string().max(100).nullable().optional(),
  notes: z.string().max(4000).nullable().optional(),
  tags: z.array(registryTagSchema).max(50).optional(),
  ownership: ownershipBlockSchema,
  validationRunId: z.string().uuid(),
});
export type OnboardingRequest = z.infer<typeof onboardingRequestSchema>;

export const updateMetadataRequestSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string().min(1).max(200),
  description: z.string().max(4000).nullable().optional(),
  businessUnit: z.string().max(200).nullable().optional(),
  productOrApplication: z.string().max(200).nullable().optional(),
  costCenter: z.string().max(100).nullable().optional(),
  notes: z.string().max(4000).nullable().optional(),
  tags: z.array(registryTagSchema).max(50).optional(),
});
export type UpdateMetadataRequest = z.infer<typeof updateMetadataRequestSchema>;

export const updateOwnershipRequestSchema = z.object({
  id: z.string().uuid(),
  ownership: ownershipBlockSchema,
});
export type UpdateOwnershipRequest = z.infer<typeof updateOwnershipRequestSchema>;

export const lifecycleTransitionRequestSchema = z
  .object({
    id: z.string().uuid(),
    action: lifecycleActionSchema,
    reason: z.string().min(1).max(1000).nullable().optional(),
  })
  .superRefine((value, ctx) => {
    if (value.action !== "enable" && !value.reason) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["reason"],
        message: "A reason is required for disable, archive, and restore actions.",
      });
    }
  });
export type LifecycleTransitionRequest = z.infer<typeof lifecycleTransitionRequestSchema>;

// === Picker / identity / inventory responses ===

export const principalPickerItemSchema = z.object({
  objectId: z.string().uuid(),
  principalType: principalTypeSchema,
  displayName: z.string(),
  mail: z.string().nullable().optional(),
  userPrincipalName: z.string().nullable().optional(),
});
export type PrincipalPickerItem = z.infer<typeof principalPickerItemSchema>;

export const workloadIdentityResponseSchema = z.object({
  principalId: z.string().uuid(),
  clientId: z.string().uuid().nullable().optional(),
  runbookUrl: z.string().url().nullable().optional(),
  sampleGrantCommand: z.string().nullable().optional(),
});
export type WorkloadIdentityResponse = z.infer<typeof workloadIdentityResponseSchema>;

export const inventoryItemSchema = onboardedNamespaceSchema;
export const inventoryPageSchema = z.object({
  items: z.array(inventoryItemSchema),
  continuationToken: z.string().nullable().optional(),
});
export type InventoryPage = z.infer<typeof inventoryPageSchema>;

// === Details response ===

// Spec 008 / contracts/namespace-onboarding-api.yaml#/OnboardedNamespaceDetails.
// Flat composition: all OnboardedNamespace fields + latestValidationRun +
// recentAuditEvents at the same top level.
export const namespaceAuditEventSchema = z.object({
  id: z.string().uuid(),
  entityId: z.string().uuid(),
  entityType: z.literal("Namespace"),
  environment: z.string(),
  eventType: z.string(),
  timestamp: z.string().datetime({ offset: true }),
  actor: z
    .object({
      principalId: z.string().nullable().optional(),
      displayName: z.string().nullable().optional(),
    })
    .nullable()
    .optional(),
  changeSummary: z.string().nullable().optional(),
  lifecycleReason: z.string().nullable().optional(),
  fieldChanges: z
    .array(
      z.object({
        field: z.string(),
        before: z.unknown().optional(),
        after: z.unknown().optional(),
      }),
    )
    .nullable()
    .optional(),
});
export type NamespaceAuditEvent = z.infer<typeof namespaceAuditEventSchema>;

export const namespaceDetailsResponseSchema = onboardedNamespaceSchema.extend({
  latestValidationRun: validationRunSchema.nullable().optional(),
  recentAuditEvents: z.array(namespaceAuditEventSchema).default([]),
});
export type NamespaceDetailsResponse = z.infer<typeof namespaceDetailsResponseSchema>;

export const validationRunListSchema = z.object({
  items: z.array(validationRunSchema),
  continuationToken: z.string().nullable().optional(),
});
export type ValidationRunList = z.infer<typeof validationRunListSchema>;
