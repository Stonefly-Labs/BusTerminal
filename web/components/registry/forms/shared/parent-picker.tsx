"use client";

/**
 * Spec 006. Parent-entity picker used by Queue / Topic / Subscription / Rule
 * forms. Looks up candidate parents in the current environment via the
 * registry API and surfaces a warning when the chosen parent is Deprecated
 * (Story 1 AC #7).
 */

import { useQuery } from "@tanstack/react-query";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { listEntities } from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";
import type { RegistryEntity, RegistryEntityType } from "@/lib/registry/types";

interface ParentPickerProps {
  readonly parentType: RegistryEntityType;
  readonly environment: string;
  readonly value: string | undefined;
  readonly onChange: (parentId: string, parent: RegistryEntity | undefined) => void;
  readonly disabled?: boolean | undefined;
}

export function ParentPicker({
  parentType,
  environment,
  value,
  onChange,
  disabled = false,
}: ParentPickerProps) {
  const parentsQuery = useQuery({
    queryKey: registryQueryKeys.entities.list({ environment, entityType: parentType }),
    queryFn: () => listEntities({ environment, entityType: parentType, pageSize: 200 }),
    enabled: !!environment,
  });

  const parents = parentsQuery.data?.items ?? [];
  const selectedParent = parents.find((p) => p.id === value);
  const deprecatedWarning =
    selectedParent && selectedParent.status === "Deprecated" ? selectedParent : undefined;

  return (
    <div className="flex flex-col gap-2" data-testid="parent-picker">
      <Label>Parent {parentType} *</Label>
      <Select
        value={value ?? ""}
        onValueChange={(v) => {
          const next = parents.find((p) => p.id === v);
          onChange(v, next);
        }}
        disabled={disabled || parents.length === 0}
      >
        <SelectTrigger aria-label={`Parent ${parentType}`}>
          <SelectValue placeholder={`Choose a parent ${parentType}`} />
        </SelectTrigger>
        <SelectContent>
          {parents.map((p) => (
            <SelectItem key={p.id} value={p.id}>
              {p.name}
              {p.status === "Deprecated" ? " (Deprecated)" : ""}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {deprecatedWarning ? (
        <Alert intent="warning" data-testid="deprecated-parent-warning">
          <AlertTitle>This parent is Deprecated</AlertTitle>
          <AlertDescription>
            New children created here will be flagged in the audit log. You can still proceed.
          </AlertDescription>
        </Alert>
      ) : null}
      {parentsQuery.isPending ? (
        <p className="text-xs text-foreground-muted">Loading parents…</p>
      ) : null}
      {parents.length === 0 && !parentsQuery.isPending ? (
        <p className="text-xs text-foreground-muted">
          No {parentType.toLowerCase()} entities in this environment yet.
        </p>
      ) : null}
    </div>
  );
}
