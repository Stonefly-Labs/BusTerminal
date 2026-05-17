"use client";

import { User } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { signOutAction } from "@/app/(auth)/sign-out-action";

export interface UserMenuUser {
  readonly name?: string | null;
  readonly email?: string | null;
}

export interface UserMenuProps {
  readonly user: UserMenuUser;
}

export function UserMenu({ user }: UserMenuProps) {
  const displayName = user.name?.trim() || user.email?.trim() || "Account";

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button intent="ghost" size="icon" aria-label={`Account menu for ${displayName}`}>
          <User />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel className="max-w-56 truncate">{displayName}</DropdownMenuLabel>
        {user.email ? (
          <DropdownMenuLabel className="max-w-56 truncate text-xs font-normal text-foreground-muted">
            {user.email}
          </DropdownMenuLabel>
        ) : null}
        <DropdownMenuSeparator />
        <form action={signOutAction}>
          <DropdownMenuItem asChild>
            <button type="submit" className="w-full text-start">
              Sign out
            </button>
          </DropdownMenuItem>
        </form>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
