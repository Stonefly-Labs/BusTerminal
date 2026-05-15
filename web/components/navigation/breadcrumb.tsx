import * as React from "react";

import {
  Breadcrumb as BreadcrumbRoot,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";

export interface BreadcrumbCrumb {
  readonly id: string;
  readonly label: string;
  readonly href?: string;
}

export interface BreadcrumbsProps {
  readonly crumbs: ReadonlyArray<BreadcrumbCrumb>;
}

/**
 * Route-aware breadcrumb composite (T086). Final crumb renders as the
 * current page, intermediate crumbs as links.
 */
export function Breadcrumbs({ crumbs }: BreadcrumbsProps) {
  return (
    <BreadcrumbRoot>
      <BreadcrumbList>
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1;
          return (
            <React.Fragment key={crumb.id}>
              <BreadcrumbItem>
                {isLast || !crumb.href ? (
                  <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                ) : (
                  <BreadcrumbLink href={crumb.href}>{crumb.label}</BreadcrumbLink>
                )}
              </BreadcrumbItem>
              {isLast ? null : <BreadcrumbSeparator />}
            </React.Fragment>
          );
        })}
      </BreadcrumbList>
    </BreadcrumbRoot>
  );
}
