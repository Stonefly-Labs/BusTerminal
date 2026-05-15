import { cva, type VariantProps } from "class-variance-authority";

/**
 * `class-variance-authority` re-export with a project-aligned signature
 * (FR-014 / FR-034). `variants` is the variant-builder primitives and
 * composites consume.
 *
 * ```ts
 * const buttonVariants = variants("inline-flex items-center …", {
 *   variants: {
 *     intent: {
 *       primary: "bg-accent-primary text-accent-primary-foreground …",
 *       ghost: "bg-transparent text-foreground-default …",
 *     },
 *     size: { sm: "h-8 px-3 text-sm", md: "h-10 px-4 text-sm" },
 *   },
 *   defaultVariants: { intent: "primary", size: "md" },
 * });
 * ```
 */
export const variants = cva;

export type VariantPropsOf<T extends (...args: never) => string> = VariantProps<T>;
