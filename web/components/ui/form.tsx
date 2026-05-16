"use client";

import * as React from "react";
import {
  Controller,
  FormProvider,
  useFormContext,
  useFormState,
  type ControllerProps,
  type FieldPath,
  type FieldValues,
} from "react-hook-form";
import { Slot } from "@radix-ui/react-slot";

import { cn } from "@/lib/design-system/cn";

import { Label } from "./label";

/**
 * Low-level RHF + Radix glue (T070). The opinionated, schema-driven `<Form>`
 * composite that consumers actually use lives in `web/components/forms/form.tsx`
 * and builds on top of these primitives.
 */
export const Form = FormProvider;

interface FormFieldContextValue<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
> {
  readonly name: TName;
}

const FormFieldContext = React.createContext<FormFieldContextValue | null>(null);

export function FormField<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
>({ ...props }: ControllerProps<TFieldValues, TName>) {
  return (
    <FormFieldContext.Provider value={{ name: props.name }}>
      <Controller {...props} />
    </FormFieldContext.Provider>
  );
}

interface FormItemContextValue {
  readonly id: string;
}

const FormItemContext = React.createContext<FormItemContextValue | null>(null);

export const FormItem = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function FormItem({ className, ...rest }, ref) {
    const id = React.useId();
    const value = React.useMemo(() => ({ id }), [id]);
    return (
      <FormItemContext.Provider value={value}>
        <div ref={ref} className={cn("flex flex-col gap-2", className)} {...rest} />
      </FormItemContext.Provider>
    );
  },
);

export function useFormField() {
  const fieldContext = React.useContext(FormFieldContext);
  const itemContext = React.useContext(FormItemContext);
  const formContext = useFormContext();
  if (!fieldContext) {
    throw new Error("useFormField must be used within a <FormField>.");
  }
  if (!itemContext) {
    throw new Error("useFormField must be used within a <FormItem>.");
  }
  if (!formContext) {
    throw new Error("useFormField must be used within a <Form> provider.");
  }
  const { id } = itemContext;
  const { errors } = useFormState({ control: formContext.control });
  const error = errors[fieldContext.name as keyof typeof errors];
  return {
    id,
    name: fieldContext.name as string,
    formItemId: `${id}-form-item`,
    formDescriptionId: `${id}-form-item-description`,
    formMessageId: `${id}-form-item-message`,
    error,
  };
}

export const FormLabel = React.forwardRef<
  React.ElementRef<typeof Label>,
  React.ComponentPropsWithoutRef<typeof Label>
>(function FormLabel({ className, ...rest }, ref) {
  const { error, formItemId } = useFormField();
  return (
    <Label
      ref={ref}
      className={cn(error ? "text-error-foreground" : undefined, className)}
      htmlFor={formItemId}
      {...rest}
    />
  );
});

export const FormControl = React.forwardRef<
  React.ElementRef<typeof Slot>,
  React.ComponentPropsWithoutRef<typeof Slot>
>(function FormControl({ ...rest }, ref) {
  const { error, formItemId, formDescriptionId, formMessageId } = useFormField();
  return (
    <Slot
      ref={ref}
      id={formItemId}
      aria-describedby={
        error ? `${formDescriptionId} ${formMessageId}` : formDescriptionId
      }
      aria-invalid={Boolean(error)}
      {...rest}
    />
  );
});

export const FormDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  function FormDescription({ className, ...rest }, ref) {
    const { formDescriptionId } = useFormField();
    return (
      <p
        ref={ref}
        id={formDescriptionId}
        className={cn("text-xs text-foreground-muted", className)}
        {...rest}
      />
    );
  },
);

export const FormMessage = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  function FormMessage({ className, children, ...rest }, ref) {
    const { error, formMessageId } = useFormField();
    const body = error ? String(error.message ?? "") : children;
    if (!body) return null;
    return (
      <p
        ref={ref}
        id={formMessageId}
        className={cn("text-xs text-error-foreground", className)}
        {...rest}
      >
        {body}
      </p>
    );
  },
);
