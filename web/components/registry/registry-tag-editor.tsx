"use client";

/**
 * Spec 006 / T091. Free-form key/value tag editor. Composes existing
 * shadcn primitives. Case-insensitive key match per `tag-utils.ts` is the
 * persistence contract — the UI persists what the operator types but
 * surfaces a hint when a case-collision is detected with already-entered
 * tags so they can fix it before submit.
 */

import { useId } from "react";
import { Trash2, Plus } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/design-system/cn";
import { tagKeyLower } from "@/lib/registry/tag-utils";
import type { RegistryTag } from "@/lib/registry/types";

interface RegistryTagEditorProps {
  readonly value: readonly RegistryTag[];
  readonly onChange: (next: readonly RegistryTag[]) => void;
  readonly className?: string;
  readonly disabled?: boolean;
}

export function RegistryTagEditor({
  value,
  onChange,
  className,
  disabled = false,
}: RegistryTagEditorProps) {
  const fieldId = useId();

  const update = (index: number, patch: Partial<RegistryTag>) => {
    const existing = value[index];
    if (!existing) return;
    const next = value.map((t, i) => (i === index ? { ...t, ...patch } : t));
    onChange(next);
  };

  const remove = (index: number) => {
    onChange(value.filter((_, i) => i !== index));
  };

  const add = () => {
    onChange([...value, { key: "", value: "" }]);
  };

  return (
    <div className={cn("flex flex-col gap-2", className)} data-testid="registry-tag-editor">
      <Label htmlFor={fieldId} className="text-sm font-medium">
        Tags
      </Label>
      <p id={`${fieldId}-help`} className="text-xs text-foreground-muted">
        Free-form key/value pairs. Keys match case-insensitively; first-write casing wins.
      </p>
      <div className="flex flex-col gap-2" id={fieldId}>
        {value.map((tag, index) => {
          const collidesWith = value.findIndex(
            (other, j) =>
              j !== index && tag.key.length > 0 && tagKeyLower(other.key) === tagKeyLower(tag.key),
          );
          return (
            <div key={index} className="flex items-center gap-2">
              <Input
                aria-label={`Tag ${index + 1} key`}
                placeholder="key"
                value={tag.key}
                onChange={(e) => update(index, { key: e.target.value })}
                disabled={disabled}
                className="max-w-[12rem]"
                data-collision={collidesWith >= 0 ? "true" : "false"}
              />
              <Input
                aria-label={`Tag ${index + 1} value`}
                placeholder="value"
                value={tag.value}
                onChange={(e) => update(index, { value: e.target.value })}
                disabled={disabled}
              />
              <Button
                type="button"
                size="sm"
                intent="ghost"
                onClick={() => remove(index)}
                disabled={disabled}
                aria-label={`Remove tag ${index + 1}`}
              >
                <Trash2 className="size-4" aria-hidden="true" />
              </Button>
            </div>
          );
        })}
        <Button
          type="button"
          size="sm"
          intent="outline"
          onClick={add}
          disabled={disabled}
          className="w-fit"
        >
          <Plus className="me-1 size-4" aria-hidden="true" />
          Add tag
        </Button>
      </div>
    </div>
  );
}
