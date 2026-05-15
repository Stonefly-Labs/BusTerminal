"use client";

import * as React from "react";
import {
  useForm,
  type DefaultValues,
  type FieldValues,
  type Resolver,
  type SubmitHandler,
  type UseFormReturn,
} from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type { ZodType } from "zod";

import { Form as RHFFormProvider } from "@/components/ui/form";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";
import type { StringKey } from "@/lib/i18n";

export interface FormProps<TValues extends FieldValues> {
  readonly schema: ZodType<TValues>;
  readonly defaultValues?: DefaultValues<TValues>;
  readonly onSubmit: SubmitHandler<TValues>;
  readonly children: (form: UseFormReturn<TValues>) => React.ReactNode;
  readonly preventDoubleSubmit?: boolean;
  readonly accessibleNameKey: StringKey;
  readonly className?: string;
}

/**
 * Schema-driven, accessible form composite (T076 / FR-017 / FR-018).
 *
 * Resolves validation through Zod, exposes the RHF instance through a
 * render-prop, and attaches `aria-label` from the i18n string surface.
 */
export function Form<TValues extends FieldValues>({
  schema,
  defaultValues,
  onSubmit,
  children,
  preventDoubleSubmit = true,
  accessibleNameKey,
  className,
}: FormProps<TValues>) {
  const form = useForm<TValues>({
    resolver: zodResolver(schema) as Resolver<TValues>,
    ...(defaultValues ? { defaultValues } : {}),
  });

  const isSubmitting = form.formState.isSubmitting;
  const disabled = preventDoubleSubmit && isSubmitting;

  return (
    <RHFFormProvider {...form}>
      <form
        aria-label={t(accessibleNameKey)}
        {...(isSubmitting ? { "aria-busy": "true" as const } : {})}
        className={cn("flex flex-col gap-4", className)}
        onSubmit={form.handleSubmit(onSubmit)}
      >
        <fieldset disabled={disabled} className="contents">
          {children(form)}
        </fieldset>
      </form>
    </RHFFormProvider>
  );
}
