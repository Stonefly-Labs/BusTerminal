"use client";

/**
 * Spec 008 / T085 + research §13. Custom 5-step indicator composed from
 * shadcn `Card` + `Badge` + framer-motion dots. No third-party stepper.
 * Honors `prefers-reduced-motion` by short-circuiting transitions.
 */

import { Check } from "lucide-react";
import { motion } from "framer-motion";

import { useReducedMotion } from "@/hooks/use-reduced-motion";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";

export interface WizardStep {
  readonly id: string;
  readonly title: string;
  readonly description?: string;
}

export interface WizardStepperProps {
  readonly steps: ReadonlyArray<WizardStep>;
  readonly currentIndex: number;
  readonly onStepClick?: (index: number) => void;
}

export function WizardStepper({ steps, currentIndex, onStepClick }: WizardStepperProps) {
  const reducedMotion = useReducedMotion();

  return (
    <Card data-testid="wizard-stepper">
      <CardContent className="p-4">
        <ol className="flex flex-col gap-2 md:flex-row md:items-center md:gap-0">
          {steps.map((step, index) => {
            const status =
              index < currentIndex ? "completed"
              : index === currentIndex ? "active"
              : "pending";
            const isClickable = onStepClick && index < currentIndex;
            return (
              <li
                key={step.id}
                className={cn(
                  "flex items-start gap-3 md:flex-1 md:items-center",
                  index !== steps.length - 1 && "md:after:mx-3 md:after:h-px md:after:flex-1 md:after:bg-border-muted md:after:content-['']",
                )}
                aria-current={status === "active" ? "step" : undefined}
              >
                <button
                  type="button"
                  disabled={!isClickable}
                  onClick={isClickable ? () => onStepClick?.(index) : undefined}
                  className={cn(
                    "flex flex-1 items-center gap-3 rounded-md p-2 text-start",
                    "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
                    isClickable
                      ? "cursor-pointer hover:bg-surface-muted"
                      : "cursor-default",
                  )}
                  data-testid={`wizard-stepper-step-${index}`}
                >
                  <StepDot
                    index={index}
                    status={status}
                    reducedMotion={reducedMotion}
                  />
                  <div className="flex flex-col">
                    <span className="text-sm font-semibold text-foreground-default">
                      {step.title}
                    </span>
                    {step.description ? (
                      <span className="text-xs text-foreground-muted">
                        {step.description}
                      </span>
                    ) : null}
                    {status === "active" ? (
                      <Badge className="mt-1 w-fit" intent="warning">
                        Current step
                      </Badge>
                    ) : null}
                  </div>
                </button>
              </li>
            );
          })}
        </ol>
      </CardContent>
    </Card>
  );
}

function StepDot({
  index,
  status,
  reducedMotion,
}: {
  readonly index: number;
  readonly status: "pending" | "active" | "completed";
  readonly reducedMotion: boolean;
}) {
  const baseClass = cn(
    "flex h-8 w-8 shrink-0 items-center justify-center rounded-full border text-xs font-semibold",
    status === "completed" && "border-success-foreground bg-success-subtle text-success-foreground",
    status === "active" && "border-primary bg-primary text-primary-foreground",
    status === "pending" && "border-border-muted bg-surface-muted text-foreground-muted",
  );
  if (reducedMotion || status !== "active") {
    return (
      <div className={baseClass} aria-hidden="true">
        {status === "completed" ? <Check className="h-4 w-4" /> : index + 1}
      </div>
    );
  }
  return (
    <motion.div
      className={baseClass}
      aria-hidden="true"
      initial={{ scale: 0.9 }}
      animate={{ scale: [0.95, 1.05, 1] }}
      transition={{ duration: 0.35, ease: "easeOut" }}
    >
      {index + 1}
    </motion.div>
  );
}
