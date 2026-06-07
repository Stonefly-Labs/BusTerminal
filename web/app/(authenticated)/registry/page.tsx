/**
 * Spec 006 / T084. Landing page (RSC). Welcomes the operator and points
 * them at the explorer / "New" CTA.
 */

import Link from "next/link";
import type { Route } from "next";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export default function RegistryLandingPage() {
  return (
    <div data-testid="registry-landing" className="mx-auto max-w-3xl flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground-default">
          Welcome to the Service Bus registry
        </h1>
        <p className="mt-2 text-foreground-muted">
          The registry is BusTerminal&apos;s authoritative catalogue of Azure Service Bus
          messaging topology — namespaces, queues, topics, subscriptions and rules across every
          environment, with ownership and tagging metadata for governance.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Get started</CardTitle>
          <CardDescription>
            Select an entity in the explorer on the left, or register your first asset.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-3">
          <Button asChild intent="primary">
            <Link href={"/registry/new/Namespace" as Route}>Register a namespace</Link>
          </Button>
          <Button asChild intent="secondary">
            <Link href={"/registry/new/Queue" as Route}>Add a queue</Link>
          </Button>
          <Button asChild intent="secondary">
            <Link href={"/registry/new/Topic" as Route}>Add a topic</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
