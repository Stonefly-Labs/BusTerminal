"use client";

/**
 * Spec 008 / T138 / US3. Client driver for the namespace edit experience.
 *
 * Loads the joined details payload via TanStack Query, gates the surface on
 * `BusTerminal.NamespaceAdministrator`, and renders tabbed
 * `<MetadataEditForm>` + `<OwnershipEditForm>`.
 */

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useHasRole } from "@/hooks/use-has-role";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

import { MetadataEditForm } from "./metadata-edit-form";
import { OwnershipEditForm } from "./ownership-edit-form";

export interface NamespaceEditClientProps {
  readonly id: string;
}

export function NamespaceEditClient({ id }: NamespaceEditClientProps) {
  const isAdmin = useHasRole("BusTerminal.NamespaceAdministrator");
  const getToken = useAcquireToken();

  const query = useQuery({
    queryKey: namespaceKeys.details(id),
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.getDetails(id, token ? { accessToken: token } : {});
    },
  });

  if (!isAdmin) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="flex flex-col gap-3 p-6">
            <h2 className="text-lg font-semibold text-foreground-default">Permission required</h2>
            <p className="text-sm text-foreground-muted">
              Editing namespace metadata or ownership requires the{" "}
              <code className="font-mono text-xs">BusTerminal.NamespaceAdministrator</code> role.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (query.isLoading) {
    return (
      <div className="p-6">
        <p className="text-sm text-foreground-muted">Loading namespace…</p>
      </div>
    );
  }

  if (query.isError || !query.data) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="flex flex-col gap-3 p-6">
            <h2 className="text-lg font-semibold text-foreground-default">Namespace unavailable</h2>
            <p className="text-sm text-foreground-muted">
              Could not load the namespace for editing. Return to inventory and try again.
            </p>
            <Button asChild intent="secondary">
              <Link href={"/namespaces" as never}>
                <ChevronLeft className="me-1 size-4" aria-hidden="true" />
                Back to inventory
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const details = query.data.details;
  const etag = query.data.etag;

  return (
    <div className="flex flex-col gap-4 p-6">
      <Link
        href={`/namespaces/${id}` as never}
        className="inline-flex items-center gap-1 text-xs text-foreground-muted hover:text-foreground-default"
      >
        <ChevronLeft className="size-3" aria-hidden="true" />
        Back to details
      </Link>

      <header>
        <h1 className="text-2xl font-semibold text-foreground-default">
          Edit {details.displayName ?? details.name}
        </h1>
        <p className="text-sm text-foreground-muted">
          Metadata changes apply immediately; ownership changes replace the entire ownership block.
        </p>
      </header>

      <Tabs defaultValue="metadata">
        <TabsList>
          <TabsTrigger value="metadata">Metadata</TabsTrigger>
          <TabsTrigger value="ownership">Ownership</TabsTrigger>
        </TabsList>
        <TabsContent value="metadata" className="pt-4">
          <MetadataEditForm namespace={details} etag={etag} />
        </TabsContent>
        <TabsContent value="ownership" className="pt-4">
          <OwnershipEditForm namespace={details} etag={etag} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
