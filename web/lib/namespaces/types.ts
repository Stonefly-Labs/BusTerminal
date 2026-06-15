/**
 * Spec 008 / T049. TypeScript types inferred from the Zod schemas in
 * `./schemas.ts`. Re-exported here so consumers can import the types
 * without pulling Zod into RSC bundles unnecessarily.
 */

export type {
  ArmResourceSnapshot,
  DriftField,
  InventoryPage,
  LifecycleAction,
  LifecycleStatus,
  LifecycleTransitionRequest,
  NamespaceAuditEvent,
  NamespaceDetailsResponse,
  OnboardedNamespace,
  OnboardingActor,
  OnboardingRequest,
  OwnershipAssignment,
  OwnershipBlock,
  OwnershipRole,
  PrincipalPickerItem,
  PrincipalType,
  RegistryTag,
  UpdateMetadataRequest,
  UpdateOwnershipRequest,
  ValidationCheckName,
  ValidationCheckOutcome,
  ValidationCheckResult,
  ValidationFailureCategory,
  ValidationRun,
  ValidationRunList,
  ValidationStatus,
  WorkloadIdentityResponse,
} from "./schemas";
