"use client";

import * as React from "react";

import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from "@/components/ui/command";
import { t } from "@/lib/i18n";

export interface CommandAction {
  readonly id: string;
  readonly label: string;
  readonly shortcut?: string;
  readonly group?: string;
  readonly onSelect: () => void;
}

export interface CommandPaletteProps {
  readonly actions: ReadonlyArray<CommandAction>;
  readonly shortcut?: { readonly key: string; readonly meta?: boolean; readonly ctrl?: boolean };
}

/**
 * Global Command Palette composite (T085). Composes the `Command` primitive
 * and binds `Cmd/Ctrl+K` by default.
 */
export function CommandPalette({ actions, shortcut = { key: "k", meta: true, ctrl: true } }: CommandPaletteProps) {
  const [open, setOpen] = React.useState(false);

  React.useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      const matchKey = event.key.toLowerCase() === shortcut.key.toLowerCase();
      const matchModifier = (shortcut.meta && event.metaKey) || (shortcut.ctrl && event.ctrlKey);
      if (matchKey && matchModifier) {
        event.preventDefault();
        setOpen((prev) => !prev);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [shortcut]);

  const grouped = React.useMemo(() => {
    const map = new Map<string, CommandAction[]>();
    for (const action of actions) {
      const key = action.group ?? "default";
      const list = map.get(key) ?? [];
      list.push(action);
      map.set(key, list);
    }
    return Array.from(map.entries());
  }, [actions]);

  return (
    <CommandDialog open={open} onOpenChange={setOpen}>
      <CommandInput placeholder={t("command.placeholder")} />
      <CommandList>
        <CommandEmpty>{t("command.empty")}</CommandEmpty>
        {grouped.map(([groupName, groupActions], index) => (
          <React.Fragment key={groupName}>
            {index > 0 ? <CommandSeparator /> : null}
            <CommandGroup heading={groupName}>
              {groupActions.map((action) => (
                <CommandItem
                  key={action.id}
                  value={action.label}
                  onSelect={() => {
                    setOpen(false);
                    action.onSelect();
                  }}
                >
                  {action.label}
                  {action.shortcut ? <CommandShortcut>{action.shortcut}</CommandShortcut> : null}
                </CommandItem>
              ))}
            </CommandGroup>
          </React.Fragment>
        ))}
      </CommandList>
    </CommandDialog>
  );
}
