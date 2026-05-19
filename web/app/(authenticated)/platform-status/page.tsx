import type { Metadata } from "next";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { apiGet } from "@/lib/api-client";
import { auth } from "@/lib/auth";

export const metadata: Metadata = {
  title: "Platform status",
};

interface WhoAmIPrincipal {
  oid: string;
  displayName: string;
  preferredUsername?: string;
  tenantId: string;
}

interface WhoAmICorrelation {
  traceId: string;
  spanId: string;
  receivedTraceparent?: string | null;
}

interface WhoAmIServer {
  environment: string;
  revision: string;
  serverTimeUtc: string;
}

interface WhoAmIResponse {
  principal: WhoAmIPrincipal;
  correlation: WhoAmICorrelation;
  server: WhoAmIServer;
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[160px_1fr] gap-3 py-1.5">
      <dt className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">{label}</dt>
      <dd className="break-all font-mono text-sm text-foreground-default">{value}</dd>
    </div>
  );
}

export default async function PlatformStatusPage() {
  const session = await auth();
  const result = await apiGet<WhoAmIResponse>(
    "/whoami",
    session?.accessToken ? { accessToken: session.accessToken } : {},
  );

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Platform status</h1>
        <p className="mt-1 text-sm text-foreground-muted">
          End-to-end check that sign-in, token validation, trace propagation, and centralized
          telemetry all work in this environment.
        </p>
      </header>

      {!result.ok ? (
        <Card>
          <CardHeader>
            <CardTitle>Could not reach the backend</CardTitle>
            <CardDescription>
              The diagnostic call returned an error. Outbound traceparent:{" "}
              <code className="font-mono text-xs">{result.traceparent}</code>
            </CardDescription>
          </CardHeader>
          <CardContent>
            <dl>
              <Field label="Error" value={result.error} />
              <Field label="Traceparent" value={result.traceparent} />
            </dl>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 lg:grid-cols-3">
          <Card data-testid="identity-card">
            <CardHeader>
              <CardTitle>Identity</CardTitle>
              <CardDescription>Resolved from the validated access token.</CardDescription>
            </CardHeader>
            <CardContent>
              <dl>
                <Field label="Display name" value={result.data.principal.displayName} />
                {result.data.principal.preferredUsername ? (
                  <Field label="UPN" value={result.data.principal.preferredUsername} />
                ) : null}
                <Field label="Object ID" value={result.data.principal.oid} />
                <Field label="Tenant ID" value={result.data.principal.tenantId} />
              </dl>
            </CardContent>
          </Card>

          <Card data-testid="correlation-card">
            <CardHeader>
              <CardTitle>Correlation</CardTitle>
              <CardDescription>W3C Trace Context echo from the backend.</CardDescription>
            </CardHeader>
            <CardContent>
              <dl>
                <Field label="Trace ID" value={result.data.correlation.traceId} />
                <Field label="Span ID" value={result.data.correlation.spanId} />
                <Field
                  label="Received traceparent"
                  value={result.data.correlation.receivedTraceparent ?? "(absent)"}
                />
                <Field label="Outbound traceparent" value={result.traceparent} />
              </dl>
            </CardContent>
          </Card>

          <Card data-testid="server-card">
            <CardHeader>
              <CardTitle>Server</CardTitle>
              <CardDescription>Backend revision metadata.</CardDescription>
            </CardHeader>
            <CardContent>
              <dl>
                <Field label="Environment" value={result.data.server.environment} />
                <Field label="Revision" value={result.data.server.revision} />
                <Field label="Server time (UTC)" value={result.data.server.serverTimeUtc} />
              </dl>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
