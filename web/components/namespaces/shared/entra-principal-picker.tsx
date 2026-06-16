"use client";

/**
 * Spec 008 / T083 + research §13. Reusable Entra principal picker.
 *
 * Composes shadcn `Popover` + `Command` (cmdk) per the canonical shadcn
 * combobox pattern — no third-party library. Debounced search via TanStack
 * Query against `GET /api/namespaces/_picker?q=...`. User/Group disambiguation
 * by leading icon. Keyboard navigation comes for free from cmdk.
 *
 * Selection is reported as a `PickedPrincipal` — the parent composes it into
 * the `OwnershipAssignment` shape with the correct `role` discriminator and
 * stamps `assignedAtUtc` / `assignedBy` at submit time.
 */

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronsUpDown, User as UserIcon, Users as GroupIcon } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import type { PrincipalPickerItem } from "@/lib/namespaces/schemas";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/design-system/cn";

export interface PickedPrincipal {
  readonly objectId: string;
  readonly principalType: "User" | "Group";
  readonly displayName: string;
}

export interface EntraPrincipalPickerProps {
  readonly label: string;
  readonly value: PickedPrincipal | null;
  readonly onChange: (value: PickedPrincipal | null) => void;
  readonly includeGroups?: boolean;
  readonly disabled?: boolean;
  readonly placeholder?: string;
  readonly required?: boolean;
  readonly testIdPrefix?: string;
}

export function EntraPrincipalPicker({
  label,
  value,
  onChange,
  includeGroups = true,
  disabled,
  placeholder = "Search users or groups",
  required,
  testIdPrefix,
}: EntraPrincipalPickerProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const getToken = useAcquireToken();

  const debouncedQuery = useDebounced(query, 250);
  const queryEnabled = debouncedQuery.trim().length > 0;
  const items = useQuery({
    queryKey: namespaceKeys.picker.search(debouncedQuery, includeGroups),
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.searchPrincipals(debouncedQuery, {
        includeGroups,
        ...(token ? { accessToken: token } : {}),
      });
    },
    enabled: queryEnabled,
    staleTime: 30_000,
  });

  const triggerLabel = value
    ? `${value.displayName}${value.principalType === "Group" ? " (group)" : ""}`
    : placeholder;

  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-sm font-medium text-foreground-default">
        {label}
        {required ? <span className="ms-0.5 text-error-foreground">*</span> : null}
      </span>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            intent="outline"
            disabled={disabled}
            data-testid={testIdPrefix ? `${testIdPrefix}-trigger` : undefined}
            aria-haspopup="listbox"
            aria-expanded={open}
            className={cn(
              "w-full justify-between",
              !value && "text-foreground-muted",
            )}
          >
            <span className="flex items-center gap-2 truncate">
              {value ? (
                value.principalType === "Group" ? (
                  <GroupIcon className="h-4 w-4" aria-hidden="true" />
                ) : (
                  <UserIcon className="h-4 w-4" aria-hidden="true" />
                )
              ) : null}
              <span className="truncate">{triggerLabel}</span>
            </span>
            <ChevronsUpDown className="ms-2 h-4 w-4 shrink-0 opacity-60" aria-hidden="true" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-[--radix-popover-trigger-width] p-0" align="start">
          <Command shouldFilter={false}>
            <CommandInput
              placeholder={placeholder}
              value={query}
              onValueChange={setQuery}
              data-testid={testIdPrefix ? `${testIdPrefix}-search` : undefined}
            />
            <CommandList>
              {!queryEnabled ? (
                <CommandEmpty>Type to search Entra directory.</CommandEmpty>
              ) : items.isPending ? (
                <CommandEmpty>Searching&hellip;</CommandEmpty>
              ) : items.isError ? (
                <CommandEmpty>Picker failed. Try again shortly.</CommandEmpty>
              ) : (items.data ?? []).length === 0 ? (
                <CommandEmpty>No matches.</CommandEmpty>
              ) : (
                <CommandGroup>
                  {(items.data ?? []).map((item) => (
                    <PickerRow
                      key={item.objectId}
                      item={item}
                      isActive={value?.objectId === item.objectId}
                      onPick={(picked) => {
                        onChange(picked);
                        setOpen(false);
                        setQuery("");
                      }}
                      testIdPrefix={testIdPrefix}
                    />
                  ))}
                </CommandGroup>
              )}
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {value ? (
        <div className="flex items-center gap-2 text-xs text-foreground-muted">
          <Badge intent={value.principalType === "Group" ? "warning" : "outline"}>
            {value.principalType}
          </Badge>
          <code className="font-mono">{value.objectId}</code>
          <Button
            type="button"
            intent="link"
            size="sm"
            onClick={() => onChange(null)}
            data-testid={testIdPrefix ? `${testIdPrefix}-clear` : undefined}
          >
            Clear
          </Button>
        </div>
      ) : null}
    </div>
  );
}

function PickerRow({
  item,
  isActive,
  onPick,
  testIdPrefix,
}: {
  readonly item: PrincipalPickerItem;
  readonly isActive: boolean;
  readonly onPick: (picked: PickedPrincipal) => void;
  readonly testIdPrefix: string | undefined;
}) {
  const subtitle = item.userPrincipalName ?? item.mail ?? null;
  return (
    <CommandItem
      value={item.objectId}
      onSelect={() =>
        onPick({
          objectId: item.objectId,
          principalType: item.principalType,
          displayName: item.displayName,
        })
      }
      data-testid={testIdPrefix ? `${testIdPrefix}-option-${item.objectId}` : undefined}
      data-active={isActive ? "true" : undefined}
      className="flex flex-col items-start gap-0 py-2"
    >
      <div className="flex items-center gap-2">
        {item.principalType === "Group" ? (
          <GroupIcon className="h-3.5 w-3.5" aria-hidden="true" />
        ) : (
          <UserIcon className="h-3.5 w-3.5" aria-hidden="true" />
        )}
        <span className="font-medium">{item.displayName}</span>
      </div>
      {subtitle ? (
        <span className="text-xs text-foreground-muted">{subtitle}</span>
      ) : null}
    </CommandItem>
  );
}

function useDebounced<T>(value: T, ms: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), ms);
    return () => window.clearTimeout(id);
  }, [value, ms]);
  return debounced;
}
