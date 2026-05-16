"use client";

import * as React from "react";
import type { Control, FieldPath, FieldValues } from "react-hook-form";

import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { t } from "@/lib/i18n";
import type { StringKey } from "@/lib/i18n";

export interface FieldProps<TValues extends FieldValues, TName extends FieldPath<TValues>> {
  readonly control: Control<TValues>;
  readonly name: TName;
  readonly labelKey: StringKey;
  readonly descriptionKey?: StringKey;
  readonly required?: boolean;
  readonly children: React.ReactNode;
}

/**
 * Composite that wires label + description + accessible required indication
 * + inline error message to a single form input (T077 / FR-017).
 *
 * Usage:
 *   <Field control={form.control} name="namespaceName" labelKey="form.field.namespaceName.label" required>
 *     <Input />
 *   </Field>
 */
export function Field<TValues extends FieldValues, TName extends FieldPath<TValues>>({
  control,
  name,
  labelKey,
  descriptionKey,
  required,
  children,
}: FieldProps<TValues, TName>) {
  return (
    <FormField
      control={control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>
            {t(labelKey)}
            {required ? (
              <span aria-hidden="true" className="text-error-foreground">*</span>
            ) : null}
            {required ? <span className="sr-only">{t("a11y.required")}</span> : null}
          </FormLabel>
          <FormControl>
            {React.cloneElement(
              children as React.ReactElement<Record<string, unknown>>,
              { ...field, ...(required ? { "aria-required": true } : {}) },
            )}
          </FormControl>
          {descriptionKey ? <FormDescription>{t(descriptionKey)}</FormDescription> : null}
          <FormMessage />
        </FormItem>
      )}
    />
  );
}
