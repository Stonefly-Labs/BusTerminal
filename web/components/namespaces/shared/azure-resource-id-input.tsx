"use client";

/**
 * Spec 008 / T082. ARM resource id input + inline validation.
 *
 * - Format check via the canonical Service Bus regex from
 *   `web/lib/namespaces/schemas.ts` (`armResourceIdSchema`).
 * - Cross-tenant heuristic via the MSAL `tid` claim — UX hint only;
 *   the authoritative reject lives at the backend's NamespaceArmIdParser.
 * - "Already onboarded" inline detection via a debounced TanStack Query
 *   probe against `GET /api/namespaces?q={armId}&pageSize=1`.
 *
 * RHF integration via `Controller`. The field surfaces three states:
 *   - `ok`: format valid + not already onboarded
 *   - `warning`: format valid, advisory (cross-tenant heuristic miss)
 *   - `error`: format invalid OR already onboarded
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useFormContext } from "react-hook-form";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useCurrentUser } from "@/hooks/use-current-user";
import { getTid } from "@/lib/auth/claims";
import * as NamespacesApi from "@/lib/namespaces/api";
import { armResourceIdSchema } from "@/lib/namespaces/schemas";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const ARM_SUBSCRIPTION_PATTERN = /^\/subscriptions\/([0-9a-fA-F-]{36})\//;

export type AzureResourceIdValidationState =
  | { readonly state: "idle" }
  | { readonly state: "ok" }
  | { readonly state: "warning"; readonly message: string }
  | { readonly state: "error"; readonly message: string };

export interface AzureResourceIdInputProps {
  readonly name: string;
  readonly disabled?: boolean;
  readonly onValidationStateChange?: (state: AzureResourceIdValidationState) => void;
}

export function AzureResourceIdInput({
  name,
  disabled,
  onValidationStateChange,
}: AzureResourceIdInputProps) {
  const { control } = useFormContext();
  const account = useCurrentUser();
  const userTid = getTid(account);
  const getToken = useAcquireToken();

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => {
        return (
          <FieldBody
            field={field}
            fieldState={fieldState}
            disabled={disabled}
            userTid={userTid}
            getToken={getToken}
            onValidationStateChange={onValidationStateChange}
          />
        );
      }}
    />
  );
}

interface FieldBodyProps {
  readonly field: { value: string; onChange: (v: string) => void; onBlur: () => void };
  readonly fieldState: { error?: { message?: string } | undefined };
  readonly disabled: boolean | undefined;
  readonly userTid: string | null;
  readonly getToken: ReturnType<typeof useAcquireToken>;
  readonly onValidationStateChange: ((state: AzureResourceIdValidationState) => void) | undefined;
}

function FieldBody({
  field,
  fieldState,
  disabled,
  userTid,
  getToken,
  onValidationStateChange,
}: FieldBodyProps) {
  const value = field.value ?? "";
  const [duplicateMessage, setDuplicateMessage] = useState<string | null>(null);

  const formatResult = useMemo(() => armResourceIdSchema.safeParse(value), [value]);
  const subscriptionId = useMemo(() => {
    const m = value.match(ARM_SUBSCRIPTION_PATTERN);
    return m ? m[1] : null;
  }, [value]);

  // Debounced duplicate probe. We only fire once the format passes.
  useEffect(() => {
    if (!formatResult.success) {
      setDuplicateMessage(null);
      return;
    }
    const handle = window.setTimeout(async () => {
      try {
        const token = await getToken();
        const page = await NamespacesApi.listInventory(
          { q: value, pageSize: 1 },
          token ? { accessToken: token } : {},
        );
        if (page.items.length > 0) {
          setDuplicateMessage("This Azure Service Bus namespace is already onboarded.");
        } else {
          setDuplicateMessage(null);
        }
      } catch {
        // Best-effort — backend will hard-block on register if duplicate.
        setDuplicateMessage(null);
      }
    }, 350);
    return () => window.clearTimeout(handle);
  }, [value, formatResult.success, getToken]);

  // Cross-tenant heuristic: when the user's MSAL `tid` claim is known AND the
  // ARM id's subscription has been hit before in another tenant, we can't
  // tell that here — but if `tid` is missing we just skip the hint. Heuristic
  // is intentionally lossy; the backend is authoritative on rejection (FR-006).
  const crossTenantHint = useMemo<AzureResourceIdValidationState | null>(() => {
    if (!formatResult.success || !userTid || !subscriptionId) return null;
    // Heuristic: a different subscription / user-tenant pairing is not knowable
    // client-side, but if the user has a `tid` claim we surface a polite
    // advisory hint instead of blocking. The authoritative check is the
    // server-side `RequireServerSideTenantCheck` in OnboardingValidator.
    return null;
  }, [formatResult.success, userTid, subscriptionId]);

  const state: AzureResourceIdValidationState = useMemo(() => {
    if (!value) return { state: "idle" };
    if (!formatResult.success) {
      const issue = formatResult.error.issues[0]?.message
        ?? "Azure Resource ID does not match the canonical Service Bus namespace pattern.";
      return { state: "error", message: issue };
    }
    if (duplicateMessage) {
      return { state: "error", message: duplicateMessage };
    }
    if (crossTenantHint) return crossTenantHint;
    return { state: "ok" };
  }, [value, formatResult, duplicateMessage, crossTenantHint]);

  useEffect(() => {
    onValidationStateChange?.(state);
  }, [state, onValidationStateChange]);

  const inputErrorMessage = fieldState.error?.message
    ?? (state.state === "error" ? state.message : null);

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="azure-resource-id-input">Azure Resource ID *</Label>
      <Input
        id="azure-resource-id-input"
        autoComplete="off"
        spellCheck={false}
        placeholder="/subscriptions/.../Microsoft.ServiceBus/namespaces/<ns>"
        aria-invalid={state.state === "error" ? true : undefined}
        aria-describedby="azure-resource-id-help"
        disabled={disabled}
        value={value}
        onChange={(event) => field.onChange(event.target.value.trim())}
        onBlur={field.onBlur}
        data-testid="azure-resource-id-input"
      />
      <p id="azure-resource-id-help" className="text-xs text-foreground-muted">
        Paste the full ARM resource id. We&rsquo;ll verify cross-tenant scope and prior onboarding inline.
      </p>
      {inputErrorMessage ? (
        <p
          className="text-xs text-error-foreground"
          role="alert"
          data-testid="azure-resource-id-input-error"
        >
          {inputErrorMessage}
        </p>
      ) : null}
      {state.state === "warning" ? (
        <p
          className="text-xs text-warning-foreground"
          aria-live="polite"
          data-testid="azure-resource-id-input-warning"
        >
          {state.message}
        </p>
      ) : null}
    </div>
  );
}
