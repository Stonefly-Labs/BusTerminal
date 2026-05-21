"use client";

import { User } from "lucide-react";
import { useCallback } from "react";

import { useResolvedRoleContext } from "@/components/auth/role-context";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useCurrentUser } from "@/hooks/use-current-user";
import { getName, getPreferredUsername } from "@/lib/auth/claims";
import { msalReady, pca } from "@/lib/auth/msal-instance";

export function UserMenu() {
  const account = useCurrentUser();
  const { effectiveRoles } = useResolvedRoleContext();
  const displayName = getName(account)?.trim() || getPreferredUsername(account)?.trim() || "Account";
  const upn = getPreferredUsername(account);
  const rolesArray = Array.from(effectiveRoles).sort();

  const handleSignOut = useCallback(() => {
    void msalReady
      .then(() => {
        const active = pca.getActiveAccount() ?? pca.getAllAccounts()[0] ?? null;
        return pca.logoutRedirect({
          account: active,
          postLogoutRedirectUri: "/",
        });
      })
      .catch(() => {
        // Soft failure; the user can retry.
      });
  }, []);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button intent="ghost" size="icon" aria-label={`Account menu for ${displayName}`}>
          <User />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel className="max-w-56 truncate">{displayName}</DropdownMenuLabel>
        {upn ? (
          <DropdownMenuLabel className="max-w-56 truncate text-xs font-normal text-foreground-muted">
            {upn}
          </DropdownMenuLabel>
        ) : null}
        <DropdownMenuSeparator />
        <DropdownMenuLabel className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">
          Roles
        </DropdownMenuLabel>
        <DropdownMenuLabel
          className="max-w-56 text-xs font-normal text-foreground-muted"
          data-testid="user-menu-roles"
        >
          {rolesArray.length === 0 ? "(none)" : rolesArray.join(", ")}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <button
            type="button"
            className="w-full text-start"
            onClick={handleSignOut}
            data-testid="user-menu-sign-out"
          >
            Sign out
          </button>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
