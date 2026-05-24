"use client";

import { forwardRef } from "react";

import { Button, type ButtonProps } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useHasRole } from "@/hooks/use-has-role";
import { authorizedRoles, type OperationClass } from "@/lib/auth/role-permission-matrix";

export interface RoleAwareButtonProps extends Omit<ButtonProps, "disabled"> {
  readonly operationClass: OperationClass;
  /** Visible action label used in the disabled-state tooltip (e.g. "Delete queue"). */
  readonly actionLabel?: string;
}

/**
 * A `<Button>` variant that becomes disabled when the caller is unauthorized
 * for the supplied operation class and surfaces an accessible tooltip naming
 * the required role(s). FR-006, WCAG 2.2 AA.
 */
export const RoleAwareButton = forwardRef<HTMLButtonElement, RoleAwareButtonProps>(
  function RoleAwareButton({ operationClass, actionLabel, children, ...rest }, ref) {
    const allowed = authorizedRoles(operationClass);
    const authorized = useHasRole(allowed);
    const rolesText = allowed.join(" or ");
    const disabledReason = `${actionLabel ?? "This action"} requires the ${rolesText} role.`;

    if (authorized) {
      return (
        <Button ref={ref} {...rest}>
          {children}
        </Button>
      );
    }

    return (
      <TooltipProvider delayDuration={150}>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="inline-block">
              <Button
                ref={ref}
                {...rest}
                disabled
                aria-disabled="true"
                aria-describedby={undefined}
                data-testid="role-aware-button-disabled"
                data-required-roles={allowed.join(",")}
              >
                {children}
              </Button>
            </span>
          </TooltipTrigger>
          <TooltipContent role="tooltip">{disabledReason}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  },
);
