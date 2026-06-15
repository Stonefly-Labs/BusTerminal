/**
 * Spec 008 / T091. Shared types for the wizard's RHF form values + per-step
 * navigation contract.
 *
 * The wizard owns one `useForm<WizardFormValues>` instance spanning all five
 * steps so step-navigation back/forward preserves input without per-step
 * state lifting. The `namespaceId` is pre-allocated at the start of step 4
 * (research §18) and re-used as both the ValidationRun.namespaceId AND the
 * eventually-registered namespace document's `id`.
 */

import type { ValidationRun } from "@/lib/namespaces/schemas";
import type { PickedPrincipal } from "@/components/namespaces/shared/entra-principal-picker";

export interface WizardFormValues {
  azureResourceId: string;
  displayName: string;
  description: string;
  environment: string;
  businessUnit: string;
  productOrApplication: string;
  costCenter: string;
  notes: string;
  primaryOwner: PickedPrincipal | null;
  secondaryOwners: PickedPrincipal[];
  technicalStewards: PickedPrincipal[];
  supportContacts: PickedPrincipal[];
  // Pre-allocated at the start of step 4 — never set by the user.
  namespaceId: string;
  validationRunId: string;
}

export const INITIAL_WIZARD_VALUES: WizardFormValues = {
  azureResourceId: "",
  displayName: "",
  description: "",
  environment: "",
  businessUnit: "",
  productOrApplication: "",
  costCenter: "",
  notes: "",
  primaryOwner: null,
  secondaryOwners: [],
  technicalStewards: [],
  supportContacts: [],
  namespaceId: "",
  validationRunId: "",
};

export interface StepProps {
  readonly goNext: () => void;
  readonly goBack: () => void;
  readonly setValidationRun: (run: ValidationRun) => void;
  readonly validationRun: ValidationRun | null;
}
