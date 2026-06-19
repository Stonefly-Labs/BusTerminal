"use client";

/**
 * Spec 009 / T111 / US4.
 *
 * Modal editor for a published entity's M:N service associations. Operators
 * can view current associations, add a new (serviceId, role) pair, and
 * remove existing ones. Uses TanStack Query for optimistic updates and
 * surfaces 409 (duplicate triple) + 412 (stale ETag) inline.
 *
 * Server is the authoritative enforcer of R-15. Client-side, the trigger
 * affordance is rendered only when `canEditEntityMetadata` returns true;
 * this component is reached only after that gate.
 */

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm, type SubmitHandler } from "react-hook-form";
import { Trash2 } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
// Native <select> is used here intentionally — the shadcn `Select` exposes
// only a controlled API (value + onValueChange) and pairing that with RHF's
// `form.watch()` trips the react-hooks/incompatible-library rule. A native
// select inherits all the keyboard semantics WCAG asks for and registers
// cleanly via `form.register`.
import { useAcquireToken } from "@/hooks/use-acquire-token";
import {
  addEntityAssociation,
  DiscoveryApiError,
  listEntityAssociations,
  removeEntityAssociation,
} from "@/lib/discovery/api";
import {
  ENTITY_SERVICE_ROLES,
  type AddAssociationRequest,
  type EntityServiceAssociation,
} from "@/lib/discovery/schemas";

interface ServiceAssociationEditorProps {
  readonly entityId: string;
  /** Initial associations (avoids a round-trip on first open). */
  readonly initialAssociations: readonly EntityServiceAssociation[];
  /** ETag for the current entity; must be supplied to every mutation. */
  readonly etag: string;
  /** Called whenever a mutation succeeds — page should refresh the entity. */
  readonly onMutated: (newEtag: string) => void;
  /** Optional trigger override. Defaults to a "Manage associations" button. */
  readonly trigger?: React.ReactNode;
}

export function ServiceAssociationEditor({
  entityId,
  initialAssociations,
  etag,
  onMutated,
  trigger,
}: ServiceAssociationEditorProps) {
  const [open, setOpen] = useState(false);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger ?? (
          <Button intent="secondary" data-testid="service-associations-trigger">
            Manage associations
          </Button>
        )}
      </DialogTrigger>
      <DialogContent
        className="max-w-lg"
        data-testid="service-associations-dialog"
        aria-describedby="service-associations-description"
      >
        <DialogHeader>
          <DialogTitle>Service associations</DialogTitle>
          <DialogDescription id="service-associations-description">
            Link this entity to one or more services and the role each service plays.
          </DialogDescription>
        </DialogHeader>
        {open ? (
          <EditorBody
            entityId={entityId}
            initialAssociations={initialAssociations}
            etag={etag}
            onMutated={onMutated}
          />
        ) : null}
        <DialogFooter>
          <DialogClose asChild>
            <Button intent="ghost">Close</Button>
          </DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

interface EditorBodyProps {
  readonly entityId: string;
  readonly initialAssociations: readonly EntityServiceAssociation[];
  readonly etag: string;
  readonly onMutated: (newEtag: string) => void;
}

function EditorBody({ entityId, initialAssociations, etag, onMutated }: EditorBodyProps) {
  const getToken = useAcquireToken();
  const queryClient = useQueryClient();
  // Derived-from-prop pattern: reset the internal etag when the prop
  // changes by remounting (key={etag} at the call-site is unnecessary
  // because the surrounding dialog body is re-rendered with the new etag
  // when the parent passes one down). Internal state advances on successful
  // mutations via setCurrentEtag in the success handlers.
  const [currentEtag, setCurrentEtag] = useState(etag);
  const [serverError, setServerError] = useState<string | null>(null);

  const associationsQuery = useQuery({
    queryKey: ["discovery", "entity", entityId, "associations"] as const,
    queryFn: async () => {
      const token = await getToken();
      return listEntityAssociations(entityId, token ? { accessToken: token } : {});
    },
    initialData: initialAssociations as EntityServiceAssociation[],
    // initialData is authoritative until a mutation invalidates the query —
    // matches how the entity-detail page already has the associations in
    // hand from the parent fetch and there's no need to round-trip on open.
    staleTime: Infinity,
  });

  const addMutation = useMutation({
    mutationFn: async (body: AddAssociationRequest) => {
      const token = await getToken();
      return addEntityAssociation(entityId, currentEtag, body, token ? { accessToken: token } : {});
    },
    onSuccess: async (result) => {
      setServerError(null);
      if ("ok" in result && result.ok === false) {
        if (result.conflict.status === 409) {
          setServerError("That service/role pairing already exists on this entity.");
          return;
        }
        if (result.conflict.status === 412) {
          setServerError("The entity changed in the background — refresh and try again.");
          return;
        }
      }
      await queryClient.invalidateQueries({ queryKey: ["discovery", "entity", entityId] });
      const newAssociations = await listEntityAssociations(entityId, await getTokenedOptions(getToken));
      queryClient.setQueryData(
        ["discovery", "entity", entityId, "associations"],
        newAssociations,
      );
      // Refresh ETag via the entity detail query if the page has wired it;
      // otherwise the next mutation will surface a 412 and force a refetch.
      const refreshed = queryClient.getQueryData<{ etag?: string }>([
        "discovery",
        "entity",
        entityId,
      ]);
      if (refreshed?.etag) {
        setCurrentEtag(refreshed.etag);
        onMutated(refreshed.etag);
      }
    },
    onError: (err) => {
      setServerError(extractErrorMessage(err));
    },
  });

  const removeMutation = useMutation({
    mutationFn: async (associationId: string) => {
      const token = await getToken();
      await removeEntityAssociation(
        entityId,
        associationId,
        currentEtag,
        token ? { accessToken: token } : {},
      );
      return associationId;
    },
    onSuccess: async () => {
      setServerError(null);
      await queryClient.invalidateQueries({ queryKey: ["discovery", "entity", entityId] });
      const newAssociations = await listEntityAssociations(entityId, await getTokenedOptions(getToken));
      queryClient.setQueryData(
        ["discovery", "entity", entityId, "associations"],
        newAssociations,
      );
      const refreshed = queryClient.getQueryData<{ etag?: string }>([
        "discovery",
        "entity",
        entityId,
      ]);
      if (refreshed?.etag) {
        setCurrentEtag(refreshed.etag);
        onMutated(refreshed.etag);
      }
    },
    onError: (err) => {
      setServerError(extractErrorMessage(err));
    },
  });

  const form = useForm<AddAssociationRequest>({
    defaultValues: { serviceId: "", role: "Consumer" },
  });

  const onSubmit: SubmitHandler<AddAssociationRequest> = async (data) => {
    await addMutation.mutateAsync(data);
    if (!serverError) {
      form.reset({ serviceId: "", role: "Consumer" });
    }
  };

  const associations = associationsQuery.data ?? [];

  return (
    <div className="flex flex-col gap-4" data-testid="service-associations-body">
      {serverError ? (
        <Alert intent="error" data-testid="service-associations-error">
          <AlertDescription>{serverError}</AlertDescription>
        </Alert>
      ) : null}

      <section aria-label="Current associations">
        <h3 className="mb-2 text-sm font-semibold text-foreground-default">Current</h3>
        {associations.length === 0 ? (
          <p className="text-sm text-foreground-muted" data-testid="associations-empty">
            No services associated yet.
          </p>
        ) : (
          <ul className="flex flex-col gap-1" data-testid="associations-list">
            {associations.map((assoc) => (
              <li
                key={assoc.associationId}
                className="flex items-center justify-between rounded-md border border-border-default px-3 py-2"
                data-testid={`association-${assoc.associationId}`}
              >
                <span className="flex items-center gap-2 text-sm">
                  <span className="font-mono">{assoc.serviceId}</span>
                  <Badge>{assoc.role}</Badge>
                </span>
                <Button
                  intent="ghost"
                  size="icon"
                  onClick={() => removeMutation.mutate(assoc.associationId)}
                  aria-label={`Remove ${assoc.serviceId} (${assoc.role})`}
                  data-testid={`remove-${assoc.associationId}`}
                  disabled={removeMutation.isPending}
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <form
        onSubmit={form.handleSubmit(onSubmit)}
        className="flex flex-col gap-3 border-t border-border-default pt-4"
        data-testid="add-association-form"
        aria-label="Add a service association"
      >
        <h3 className="text-sm font-semibold text-foreground-default">Add association</h3>
        <div className="flex flex-col gap-1">
          <Label htmlFor="add-service-id">Service ID</Label>
          <Input
            id="add-service-id"
            placeholder="svc_…"
            {...form.register("serviceId", {
              required: "Service ID is required.",
              minLength: { value: 1, message: "Service ID is required." },
            })}
            data-testid="add-service-id-input"
            aria-invalid={form.formState.errors.serviceId ? true : undefined}
          />
          {form.formState.errors.serviceId ? (
            <p className="text-xs text-destructive" role="alert">
              {form.formState.errors.serviceId.message}
            </p>
          ) : null}
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="add-role">Role</Label>
          <select
            id="add-role"
            data-testid="add-role-select"
            className="h-9 rounded-md border border-border-default bg-surface-elevated px-3 text-sm text-foreground-default"
            {...form.register("role")}
          >
            {ENTITY_SERVICE_ROLES.map((role) => (
              <option key={role} value={role} data-testid={`add-role-${role}`}>
                {role}
              </option>
            ))}
          </select>
        </div>
        <Button
          type="submit"
          disabled={addMutation.isPending}
          data-testid="add-association-submit"
        >
          {addMutation.isPending ? "Adding…" : "Add association"}
        </Button>
      </form>
    </div>
  );
}

async function getTokenedOptions(
  getToken: () => Promise<string | null>,
): Promise<{ accessToken?: string }> {
  const token = await getToken();
  return token ? { accessToken: token } : {};
}

function extractErrorMessage(err: unknown): string {
  if (err instanceof DiscoveryApiError) {
    if (err.status === 409) return "That service/role pairing already exists on this entity.";
    if (err.status === 412) return "The entity changed in the background — refresh and try again.";
    if (err.status === 403) return "You are not authorized to manage associations on this entity.";
    return `Request failed (${err.status}).`;
  }
  if (err instanceof Error) return err.message;
  return "Unknown error";
}
