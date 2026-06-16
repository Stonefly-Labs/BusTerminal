/**
 * Spec 008 / T111 / US2 / FR-011. Ownership panel — renders the four
 * ownership roles (PrimaryOwner, SecondaryOwner, TechnicalSteward,
 * SupportContact) with Entra display names. Flags entries where the rendered
 * name is the snapshot only (i.e., Graph re-resolution failed or was skipped).
 *
 * RSC-safe (pure presentational).
 */

import { CircleAlert, Users, User } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  resolveOwnershipBlock,
  type ResolvedOwnershipAssignment,
} from "@/lib/namespaces/ownership-resolution";
import type { OwnershipBlock } from "@/lib/namespaces/types";

interface NamespaceOwnershipPanelProps {
  readonly ownership: OwnershipBlock | null | undefined;
  readonly onboardingActor?: { readonly displayNameSnapshot: string; readonly onboardedAtUtc: string } | null | undefined;
}

export function NamespaceOwnershipPanel({ ownership, onboardingActor }: NamespaceOwnershipPanelProps) {
  if (!ownership) {
    return (
      <Card data-testid="namespace-ownership-panel">
        <CardHeader>
          <CardTitle>Ownership</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-foreground-muted">
          No structured ownership recorded for this namespace.
        </CardContent>
      </Card>
    );
  }

  const resolved = resolveOwnershipBlock(ownership);

  return (
    <Card data-testid="namespace-ownership-panel">
      <CardHeader>
        <CardTitle>Ownership</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <RoleSection label="Primary owner" assignments={[resolved.primaryOwner]} singular />
        <RoleSection label="Secondary owners" assignments={resolved.secondaryOwners} />
        <RoleSection label="Technical stewards" assignments={resolved.technicalStewards} />
        <RoleSection label="Support contacts" assignments={resolved.supportContacts} />

        {onboardingActor ? (
          <div className="border-t border-border-default pt-4 text-xs text-foreground-muted">
            Onboarded by{" "}
            <span className="font-medium text-foreground-default">
              {onboardingActor.displayNameSnapshot}
            </span>{" "}
            on {new Date(onboardingActor.onboardedAtUtc).toLocaleString()}.
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function RoleSection({
  label,
  assignments,
  singular,
}: {
  label: string;
  assignments: ReadonlyArray<ResolvedOwnershipAssignment>;
  singular?: boolean;
}) {
  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-foreground-subtle">{label}</h3>
      {assignments.length === 0 ? (
        <p className="text-sm text-foreground-muted">{singular ? "—" : "None assigned."}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {assignments.map((a) => (
            <li key={a.objectId} className="flex items-center gap-2 text-sm">
              {a.principalType === "Group" ? (
                <Users className="size-4 text-foreground-muted" aria-hidden="true" />
              ) : (
                <User className="size-4 text-foreground-muted" aria-hidden="true" />
              )}
              <span className="font-medium text-foreground-default">{a.displayNameResolved}</span>
              <Badge intent="outline">{a.principalType}</Badge>
              {a.displayNameIsSnapshotOnly ? (
                <span
                  title="Name shown from the captured snapshot — Microsoft Graph re-resolution is not available."
                  className="inline-flex items-center gap-1 text-xs text-foreground-subtle"
                >
                  <CircleAlert className="size-3" aria-hidden="true" />
                  snapshot
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
