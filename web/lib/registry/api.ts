/**
 * Spec 006 / T053. Typed registry API client.
 *
 * Composes `web/lib/http/client.ts` (which already propagates W3C Trace
 * Context per FR-042) so every UI-originated request carries `traceparent`.
 * Strongly-typed via Zod schemas — every response is parsed before being
 * returned so calling code gets compile-time + runtime guarantees.
 *
 * RSC-safe: this module has no React imports and avoids the MSAL acquire
 * dance. RSC callers pass the access token explicitly via `options.accessToken`;
 * Client Component callers compose `useApiToken` (FR-042 pattern) and pass
 * the result in. The hook layer lives in the page/form components.
 */

import { resolveMockRolesHeaderValue } from "@/lib/api-client";
import { httpFetch, type HttpFetchOptions } from "@/lib/http/client";
import { E2E_MOCK_ROLES_HEADER } from "@/tests/auth/personas";
import {
  auditEventSchema,
  conflictResponseSchema,
  hasChildrenResponseSchema,
  registryEntityPageSchema,
  registryEntitySchema,
  registrySearchResponseSchema,
  type AuditEvent,
  type ConflictResponse,
  type HasChildrenResponse,
  type RegistryEntity,
  type RegistryEntityCreateRequest,
  type RegistryEntityPage,
  type RegistryEntityType,
  type RegistryEntityUpdateRequest,
  type RegistrySearchResponse,
} from "./schemas";

const DEFAULT_BASE_URL = "/api/registry";

export interface RegistryApiOptions {
  readonly accessToken?: string;
  readonly signal?: AbortSignal;
  readonly baseUrl?: string;
}

export interface RegistryListParams {
  readonly environment: string;
  readonly entityType?: RegistryEntityType;
  readonly parentId?: string;
  readonly status?: string;
  readonly pageSize?: number;
  readonly continuationToken?: string;
}

export interface RegistrySearchParams {
  readonly query?: string;
  readonly entityType?: RegistryEntityType;
  readonly environment?: string;
  readonly status?: string;
  readonly tagKeysAnyLower?: readonly string[];
  readonly skip?: number;
  readonly top?: number;
}

export interface RegistryUpdateResult {
  readonly ok: true;
  readonly entity: RegistryEntity;
  readonly etag: string;
}

export interface RegistryUpdateConflict {
  readonly ok: false;
  readonly conflict: ConflictResponse;
}

export interface RegistryDeleteBlocked {
  readonly ok: false;
  readonly hasChildren: HasChildrenResponse;
}

export class RegistryApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body?: unknown,
  ) {
    super(message);
    this.name = "RegistryApiError";
  }
}

function buildHeaders(options: RegistryApiOptions, init?: HeadersInit): Headers {
  const headers = new Headers(init);
  headers.set("accept", "application/json");
  if (options.accessToken) {
    headers.set("authorization", `Bearer ${options.accessToken}`);
  }
  // Spec 007 — mock-auth E2E personas. The shared helper returns null
  // outside mock mode, so this is a no-op for real-token callers.
  const mockRoles = resolveMockRolesHeaderValue();
  if (mockRoles !== null) {
    headers.set(E2E_MOCK_ROLES_HEADER, mockRoles);
  }
  return headers;
}

function baseUrlFor(options: RegistryApiOptions): string {
  return options.baseUrl ?? DEFAULT_BASE_URL;
}

/**
 * Resolve the `RegistryApiOptions` to use for a read call, ensuring a bearer
 * token is attached. This client never acquires its own token — callers must
 * supply one (FR-037 AuthN-only). Client components pass `useAcquireToken`'s
 * resolver as `getToken`; if `apiOptions` already carries an `accessToken`
 * (e.g. an RSC caller or a test) it is used as-is and `getToken` is skipped.
 *
 * Kept here (not in the `use-acquire-token` hook) so this module stays
 * RSC-safe — it takes a plain token getter rather than importing the hook.
 */
export async function resolveApiOptions(
  apiOptions: RegistryApiOptions | undefined,
  getToken: () => Promise<string | null>,
): Promise<RegistryApiOptions> {
  if (apiOptions?.accessToken) return apiOptions;
  const token = await getToken();
  return token ? { ...apiOptions, accessToken: token } : (apiOptions ?? {});
}

// `exactOptionalPropertyTypes` distinguishes "key absent" from "key:
// undefined" — strip undefined values so HttpFetchOptions.signal stays
// typed as AbortSignal | null without leaking undefined into it.
function makeFetchInit(
  options: RegistryApiOptions,
  headers: Headers,
  operation: string,
  extras: Omit<HttpFetchOptions, "headers" | "operation" | "signal"> = {},
): HttpFetchOptions {
  const init: HttpFetchOptions = { headers, operation, ...extras };
  if (options.signal) {
    init.signal = options.signal;
  }
  return init;
}

async function readJsonOrThrow(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined;
  const text = await response.text();
  if (!text) return undefined;
  return JSON.parse(text) as unknown;
}

export async function listEntities(
  params: RegistryListParams,
  options: RegistryApiOptions = {},
): Promise<RegistryEntityPage> {
  const search = new URLSearchParams();
  search.set("environment", params.environment);
  if (params.entityType) search.set("entityType", params.entityType);
  if (params.parentId) search.set("parentId", params.parentId);
  if (params.status) search.set("status", params.status);
  if (params.pageSize) search.set("pageSize", String(params.pageSize));
  if (params.continuationToken) search.set("continuationToken", params.continuationToken);

  const url = `${baseUrlFor(options)}?${search.toString()}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "registry.list"),
  );
  if (!response.ok) {
    throw new RegistryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return registryEntityPageSchema.parse(await readJsonOrThrow(response));
}

export interface RegistryEntityWithEtag {
  readonly entity: RegistryEntity;
  readonly etag: string;
}

export async function getEntity(
  id: string,
  options: RegistryApiOptions = {},
): Promise<RegistryEntityWithEtag | null> {
  const url = `${baseUrlFor(options)}/${id}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "registry.get"),
  );
  if (response.status === 404) return null;
  if (!response.ok) {
    throw new RegistryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const entity = registryEntitySchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? entity.id;
  return { entity, etag };
}

export async function createEntity(
  body: RegistryEntityCreateRequest,
  options: RegistryApiOptions = {},
): Promise<RegistryEntityWithEtag> {
  const url = baseUrlFor(options);
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "registry.create", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  );
  if (!response.ok) {
    throw new RegistryApiError(`POST ${url} → ${response.status}`, response.status, await response.text());
  }
  const entity = registryEntitySchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? entity.id;
  return { entity, etag };
}

export async function updateEntity(
  id: string,
  body: RegistryEntityUpdateRequest,
  ifMatchEtag: string,
  options: RegistryApiOptions = {},
): Promise<RegistryUpdateResult | RegistryUpdateConflict> {
  const url = `${baseUrlFor(options)}/${id}`;
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "registry.update", {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  );
  if (response.status === 409) {
    const parsed = conflictResponseSchema.parse(await readJsonOrThrow(response));
    return { ok: false, conflict: parsed };
  }
  if (!response.ok) {
    throw new RegistryApiError(`PUT ${url} → ${response.status}`, response.status, await response.text());
  }
  const entity = registryEntitySchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? entity.id;
  return { ok: true, entity, etag };
}

export async function deleteEntity(
  id: string,
  ifMatchEtag: string,
  options: RegistryApiOptions = {},
): Promise<{ ok: true } | RegistryDeleteBlocked> {
  const url = `${baseUrlFor(options)}/${id}`;
  const headers = buildHeaders(options);
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "registry.delete", { method: "DELETE" }),
  );
  if (response.status === 409) {
    const parsed = hasChildrenResponseSchema.parse(await readJsonOrThrow(response));
    return { ok: false, hasChildren: parsed };
  }
  if (!response.ok) {
    throw new RegistryApiError(`DELETE ${url} → ${response.status}`, response.status, await response.text());
  }
  return { ok: true };
}

export async function searchEntities(
  params: RegistrySearchParams,
  options: RegistryApiOptions = {},
): Promise<RegistrySearchResponse> {
  const search = new URLSearchParams();
  if (params.query) search.set("q", params.query);
  if (params.entityType) search.set("entityType", params.entityType);
  if (params.environment) search.set("environment", params.environment);
  if (params.status) search.set("status", params.status);
  if (params.tagKeysAnyLower) {
    for (const k of params.tagKeysAnyLower) search.append("tagKey", k);
  }
  if (params.skip !== undefined) search.set("skip", String(params.skip));
  if (params.top !== undefined) search.set("top", String(params.top));

  const url = `${baseUrlFor(options)}/search?${search.toString()}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "registry.search"),
  );
  if (!response.ok) {
    throw new RegistryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return registrySearchResponseSchema.parse(await readJsonOrThrow(response));
}

export async function listAuditForEntity(
  entityId: string,
  limit: number = 50,
  options: RegistryApiOptions = {},
): Promise<readonly AuditEvent[]> {
  const url = `${baseUrlFor(options)}/${entityId}/audit?limit=${limit}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "registry.audit.list"),
  );
  if (!response.ok) {
    throw new RegistryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const json = (await readJsonOrThrow(response)) as { items?: unknown[] };
  const items = json?.items ?? [];
  return items.map((it) => auditEventSchema.parse(it));
}

export async function listEnvironments(
  options: RegistryApiOptions = {},
): Promise<readonly string[]> {
  const url = `${baseUrlFor(options)}/environments`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "registry.environments"),
  );
  if (!response.ok) {
    throw new RegistryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const json = (await readJsonOrThrow(response)) as { items?: readonly string[] } | undefined;
  return json?.items ?? [];
}

// Allow callers to compose with extra fetch options when needed (e.g. an
// abort controller, custom operation name for telemetry tracing).
export type RegistryHttpOptions = HttpFetchOptions;
