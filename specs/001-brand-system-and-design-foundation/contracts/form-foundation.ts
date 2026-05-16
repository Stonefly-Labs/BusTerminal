/**
 * Form Foundation Contract
 *
 * Spec references:
 *   - FR-017 (schema-driven validation, accessible required indication,
 *     accessible inline error display, submit-pending state, destructive
 *     confirmation pattern)
 *   - FR-018 (long-running operations provide progress / clear feedback)
 *
 * Implementation: React Hook Form + Zod. Wrapped in BusTerminal-owned
 * field composites under `web/components/forms/`.
 */

import type { z } from 'zod';
import type { ReactNode } from 'react';
import type {
  Control,
  FieldPath,
  FieldValues,
  SubmitHandler,
  UseFormReturn,
} from 'react-hook-form';

export interface FormProps<TSchema extends z.ZodTypeAny> {
  /** Zod schema defines validation rules (FR-017). */
  readonly schema: TSchema;
  /** Default values; partial allowed. */
  readonly defaultValues?: Partial<z.infer<TSchema>>;
  /** Submit handler — receives validated data. */
  readonly onSubmit: SubmitHandler<z.infer<TSchema>>;
  /** Children render-prop receives the form instance. */
  readonly children: (form: UseFormReturn<z.infer<TSchema>>) => ReactNode;
  /** When true, the submit button is disabled and shows a spinner while submitting (FR-017). */
  readonly preventDoubleSubmit?: boolean; // default: true
  /** Sourced from i18n string surface; used as the accessible name for the form. */
  readonly accessibleNameKey: string;
}

export interface FieldProps<TValues extends FieldValues, TName extends FieldPath<TValues>> {
  readonly control: Control<TValues>;
  readonly name: TName;
  readonly labelKey: string; // i18n string key — never raw text
  readonly descriptionKey?: string;
  readonly required?: boolean; // visually and programmatically conveyed (FR-017)
  readonly children: ReactNode;
}

export interface DestructiveConfirmationProps {
  readonly titleKey: string;
  readonly descriptionKey: string;
  readonly confirmLabelKey: string;
  readonly cancelLabelKey: string;
  readonly onConfirm: () => Promise<void> | void;
}

/**
 * The `Form` component takes the Zod schema, threads it through React Hook
 * Form, and exposes the `useFormReturn` to children. Errors render inline
 * using an accessible pattern that connects the error message to the
 * input via `aria-describedby`.
 */
export declare function Form<TSchema extends z.ZodTypeAny>(
  props: FormProps<TSchema>,
): JSX.Element;

export declare function Field<TValues extends FieldValues, TName extends FieldPath<TValues>>(
  props: FieldProps<TValues, TName>,
): JSX.Element;

/**
 * Triggers a confirmation modal before invoking the destructive action.
 * Returns a stable callback ready to use as an event handler.
 */
export declare function useDestructiveConfirm(
  props: DestructiveConfirmationProps,
): () => void;

/**
 * Long-running submission helper (FR-018): wraps the submit promise and
 * surfaces progress states via the toast surface and the form's
 * `isSubmitting` flag.
 */
export declare function useLongRunningSubmit<TValues>(opts: {
  readonly onSubmit: SubmitHandler<TValues>;
  readonly progressLabelKey: string;
  readonly successLabelKey: string;
  readonly errorCategory: 'data-fetch' | 'render' | 'route-load';
}): SubmitHandler<TValues>;
