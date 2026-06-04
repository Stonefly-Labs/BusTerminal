"use client";

/**
 * Spec 006 / T093. ARM-resource-id text input with inline parse/validate. The
 * authoritative validation lives in the backend FluentValidator
 * (RegistryEntityValidationRules.AzureResourceIdFormat) — this helper is
 * cosmetic guidance for the form layer.
 */

import { useId } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/design-system/cn";
import type { RegistryEntityType } from "@/lib/registry/types";

interface AzureResourceIdInputProps {
  readonly entityType: RegistryEntityType;
  readonly value: string;
  readonly onChange: (next: string) => void;
  readonly disabled?: boolean;
  readonly className?: string;
}

const ARM_BASE_PATTERN =
  /^\/subscriptions\/[0-9a-fA-F-]{36}\/resourceGroups\/[^/]+\/providers\/[^/]+\/[^/]+\/[^/]+(\/[^/]+\/[^/]+)*$/;

const TYPE_SEGMENT: Record<RegistryEntityType, string> = {
  Namespace: "namespaces",
  Queue: "queues",
  Topic: "topics",
  Subscription: "subscriptions",
  Rule: "rules",
};

export function isLikelyValidArmId(value: string, entityType: RegistryEntityType): boolean {
  if (!value) return true; // optional
  if (!ARM_BASE_PATTERN.test(value)) return false;
  const segments = value.split("/").filter(Boolean);
  if (segments.length < 2) return false;
  const expected = TYPE_SEGMENT[entityType];
  return segments[segments.length - 2]?.toLowerCase() === expected;
}

export function AzureResourceIdInput({
  entityType,
  value,
  onChange,
  disabled = false,
  className,
}: AzureResourceIdInputProps) {
  const fieldId = useId();
  const trimmed = value.trim();
  const valid = isLikelyValidArmId(trimmed, entityType);
  const showError = trimmed.length > 0 && !valid;

  return (
    <div className={cn("flex flex-col gap-1", className)} data-testid="azure-resource-id-input">
      <Label htmlFor={fieldId} className="text-sm font-medium">
        Azure resource id
      </Label>
      <Input
        id={fieldId}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        placeholder={`/subscriptions/.../providers/Microsoft.ServiceBus/${TYPE_SEGMENT[entityType]}/...`}
        aria-invalid={showError || undefined}
        aria-describedby={showError ? `${fieldId}-error` : undefined}
        className="font-mono text-xs"
      />
      {showError ? (
        <p id={`${fieldId}-error`} className="text-xs text-error-foreground">
          Doesn&apos;t look like a valid ARM id for a {entityType.toLowerCase()}. The path must end
          with <code className="font-mono">/{TYPE_SEGMENT[entityType]}/&lt;name&gt;</code>.
        </p>
      ) : null}
    </div>
  );
}
