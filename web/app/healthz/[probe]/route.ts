import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

// Container Apps issues the startup / liveness / readiness probes against
// /healthz/{startup,live,ready} (see iac/modules/container-app/main.tf).
// Each probe just needs a fast 200 from this replica — no downstream calls
// (a frontend that depends on the backend to be "ready" creates a circular
// dependency at deploy time).
export const dynamic = "force-dynamic";

const VALID_PROBES = new Set(["live", "ready", "startup"]);

export async function GET(
  _request: NextRequest,
  context: { params: Promise<{ probe: string }> },
): Promise<NextResponse> {
  const { probe } = await context.params;
  if (!VALID_PROBES.has(probe)) {
    return NextResponse.json({ status: "not_found", probe }, { status: 404 });
  }
  return NextResponse.json({ status: "ok", probe }, { status: 200 });
}
