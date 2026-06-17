/**
 * Spec 009 / T026. Typed discovery + published-entity API client.
 *
 * Style-matched to `web/lib/registry/api.ts`: same options shape, same
 * mock-roles header handling, same `httpFetch` substrate (so `traceparent`
 * propagation is automatic per FR-042). All responses are Zod-parsed before
 * being returned. Non-2xx → `DiscoveryApiError`, except 409 (duplicate
 * association) and 412 (ETag mismatch) which map to typed conflict results
 * so calling code can branch cleanly.
 */

import { resolveMockRolesHeaderValue } from "@/lib/api-client";
import { httpFetch, type HttpFetchOptions } from "@/lib/http/client";
import { E2E_MOCK_ROLES_HEADER } from "@/tests/auth/personas";
import {
  addAssociationRequestSchema,
  discoveryRunPageSchema,
  discoveryRunSchema,
  entityServiceAssociationSchema,
  publishedEntitySchema,
  publishedEntitySearchResponseSchema,
  startDiscoveryResponseSchema,
  updateEntityMetadataRequestSchema,
  type AddAssociationRequest,
  type DiscoveryRun,
  type DiscoveryRunPage,
  type EntityServiceAssociation,
  type EntityServiceRole,
  type EntityType,
  type LifecycleStatus,
  type PublishedEntity,
  type PublishedEntitySearchResponse,
  type StartDiscoveryResponse,
  type UpdateEntityMetadataRequest,
} from "./schemas";

const DEFAULT_BASE_URL = "/api";

export interface DiscoveryApiOptions {
  readonly accessToken?: string;
  readonly signal?: AbortSignal;
  readonly baseUrl?: string;
}

export interface ListDiscoveryRunsParams {
  readonly pageSize?: number;
  readonly continuationToken?: string;
}

export interface SearchEntitiesParams {
  readonly q?: string;
  readonly entityType?: readonly EntityType[];
  readonly namespaceId?: string;
  readonly associatedServiceId?: string;
  readonly associationRole?: readonly EntityServiceRole[];
  readonly tag?: readonly string[];
  readonly lifecycleStatus?: readonly LifecycleStatus[];
  readonly sort?: "name_asc" | "name_desc" | "lastSeen_asc" | "lastSeen_desc";
  readonly page?: number;
  readonly pageSize?: number;
}

export interface EntityDetailResult {
  readonly entity: PublishedEntity;
  readonly etag: string;
}

export interface EntityMutationSuccess {
  readonly ok: true;
  readonly entity: PublishedEntity;
  readonly etag: string;
}

export interface EntityMutationConflict {
  readonly ok: false;
  readonly conflict: EntityConflict;
}

export interface EntityConflict {
  readonly status: 409 | 412;
  readonly body?: unknown;
}

export interface AssociationAddConflict {
  readonly ok: false;
  readonly conflict: EntityConflict;
}

export type EntityMutationResult = EntityMutationSuccess | EntityMutationConflict;

export class DiscoveryApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body?: unknown,
  ) {
    super(message);
    this.name = "DiscoveryApiError";
  }
}

function buildHeaders(options: DiscoveryApiOptions, init?: HeadersInit): Headers {
  const headers = new Headers(init);
  headers.set("accept", "application/json");
  if (options.accessToken) {
    headers.set("authorization", `Bearer ${options.accessToken}`);
  }
  const mockRoles = resolveMockRolesHeaderValue();
  if (mockRoles !== null) {
    headers.set(E2E_MOCK_ROLES_HEADER, mockRoles);
  }
  return headers;
}

function baseUrlFor(options: DiscoveryApiOptions): string {
  return options.baseUrl ?? DEFAULT_BASE_URL;
}

function makeFetchInit(
  options: DiscoveryApiOptions,
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

async function readBodySafe(response: Response): Promise<unknown> {
  try {
    return await readJsonOrThrow(response);
  } catch {
    return undefined;
  }
}

export async function startDiscovery(
  namespaceId: string,
  options: DiscoveryApiOptions = {},
): Promise<StartDiscoveryResponse> {
  const url = `${baseUrlFor(options)}/namespaces/${namespaceId}/discover`;
  const headers = buildHeaders(options);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "discovery.start", { method: "POST" }),
  );
  if (!response.ok) {
    throw new DiscoveryApiError(
      `POST ${url} → ${response.status}`,
      response.status,
      await readBodySafe(response),
    );
  }
  return startDiscoveryResponseSchema.parse(await readJsonOrThrow(response));
}

export async function getDiscoveryRun(
  discoveryRunId: string,
  namespaceId: string,
  options: DiscoveryApiOptions = {},
): Promise<DiscoveryRun> {
  const search = new URLSearchParams({ namespaceId });
  const url = `${baseUrlFor(options)}/discovery-runs/${discoveryRunId}?${search.toString()}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "discovery.get"),
  );
  if (!response.ok) {
    throw new DiscoveryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return discoveryRunSchema.parse(await readJsonOrThrow(response));
}

export async function listDiscoveryRuns(
  namespaceId: string,
  params: ListDiscoveryRunsParams = {},
  options: DiscoveryApiOptions = {},
): Promise<DiscoveryRunPage> {
  const search = new URLSearchParams();
  if (params.pageSize !== undefined) search.set("pageSize", String(params.pageSize));
  if (params.continuationToken) search.set("continuationToken", params.continuationToken);

  const qs = search.toString();
  const url = `${baseUrlFor(options)}/namespaces/${namespaceId}/discovery-runs${qs ? `?${qs}` : ""}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "discovery.list"),
  );
  if (!response.ok) {
    throw new DiscoveryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return discoveryRunPageSchema.parse(await readJsonOrThrow(response));
}

export async function searchEntities(
  params: SearchEntitiesParams,
  options: DiscoveryApiOptions = {},
): Promise<PublishedEntitySearchResponse> {
  const search = new URLSearchParams();
  if (params.q) search.set("q", params.q);
  if (params.entityType) {
    for (const t of params.entityType) search.append("entityType", t);
  }
  if (params.namespaceId) search.set("namespaceId", params.namespaceId);
  if (params.associatedServiceId) search.set("associatedServiceId", params.associatedServiceId);
  if (params.associationRole) {
    for (const r of params.associationRole) search.append("associationRole", r);
  }
  if (params.tag) {
    for (const t of params.tag) search.append("tag", t);
  }
  if (params.lifecycleStatus) {
    for (const s of params.lifecycleStatus) search.append("lifecycleStatus", s);
  }
  if (params.sort) search.set("sort", params.sort);
  if (params.page !== undefined) search.set("page", String(params.page));
  if (params.pageSize !== undefined) search.set("pageSize", String(params.pageSize));

  const url = `${baseUrlFor(options)}/entities?${search.toString()}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "entities.search"),
  );
  if (!response.ok) {
    throw new DiscoveryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return publishedEntitySearchResponseSchema.parse(await readJsonOrThrow(response));
}

export async function getEntityDetail(
  entityId: string,
  options: DiscoveryApiOptions = {},
): Promise<EntityDetailResult> {
  const url = `${baseUrlFor(options)}/entities/${entityId}`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "entities.get"),
  );
  if (!response.ok) {
    throw new DiscoveryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const entity = publishedEntitySchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? entity.etag ?? entity.id;
  return { entity, etag };
}

export async function updateEntityMetadata(
  entityId: string,
  ifMatchEtag: string,
  body: UpdateEntityMetadataRequest,
  options: DiscoveryApiOptions = {},
): Promise<EntityMutationResult> {
  const parsedBody = updateEntityMetadataRequestSchema.parse(body);
  const url = `${baseUrlFor(options)}/entities/${entityId}`;
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "entities.update", {
      method: "PATCH",
      body: JSON.stringify(parsedBody),
    }),
  );
  if (response.status === 412) {
    return { ok: false, conflict: { status: 412, body: await readBodySafe(response) } };
  }
  if (response.status === 409) {
    return { ok: false, conflict: { status: 409, body: await readBodySafe(response) } };
  }
  if (!response.ok) {
    throw new DiscoveryApiError(
      `PATCH ${url} → ${response.status}`,
      response.status,
      await readBodySafe(response),
    );
  }
  const entity = publishedEntitySchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? entity.etag ?? entity.id;
  return { ok: true, entity, etag };
}

export async function archiveEntity(
  entityId: string,
  ifMatchEtag: string,
  options: DiscoveryApiOptions = {},
): Promise<EntityMutationResult> {
  const url = `${baseUrlFor(options)}/entities/${entityId}/archive`;
  const headers = buildHeaders(options);
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "entities.archive", { method: "POST" }),
  );
  if (response.status === 412) {
    return { ok: false, conflict: { status: 412, body: await readBodySafe(response) } };
  }
  if (response.status === 409) {
    return { ok: false, conflict: { status: 409, body: await readBodySafe(response) } };
  }
  if (!response.ok) {
    throw new DiscoveryApiError(
      `POST ${url} → ${response.status}`,
      response.status,
      await readBodySafe(response),
    );
  }
  const entity = publishedEntitySchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? entity.etag ?? entity.id;
  return { ok: true, entity, etag };
}

export async function listEntityAssociations(
  entityId: string,
  options: DiscoveryApiOptions = {},
): Promise<readonly EntityServiceAssociation[]> {
  const url = `${baseUrlFor(options)}/entities/${entityId}/associations`;
  const response = await httpFetch(
    url,
    makeFetchInit(options, buildHeaders(options), "entities.associations.list"),
  );
  if (!response.ok) {
    throw new DiscoveryApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const json = (await readJsonOrThrow(response)) as unknown;
  const items = Array.isArray(json) ? json : [];
  return items.map((it) => entityServiceAssociationSchema.parse(it));
}

export async function addEntityAssociation(
  entityId: string,
  ifMatchEtag: string,
  body: AddAssociationRequest,
  options: DiscoveryApiOptions = {},
): Promise<EntityServiceAssociation | AssociationAddConflict> {
  const parsedBody = addAssociationRequestSchema.parse(body);
  const url = `${baseUrlFor(options)}/entities/${entityId}/associations`;
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "entities.associations.add", {
      method: "POST",
      body: JSON.stringify(parsedBody),
    }),
  );
  if (response.status === 409) {
    return { ok: false, conflict: { status: 409, body: await readBodySafe(response) } };
  }
  if (response.status === 412) {
    return { ok: false, conflict: { status: 412, body: await readBodySafe(response) } };
  }
  if (!response.ok) {
    throw new DiscoveryApiError(
      `POST ${url} → ${response.status}`,
      response.status,
      await readBodySafe(response),
    );
  }
  return entityServiceAssociationSchema.parse(await readJsonOrThrow(response));
}

export async function removeEntityAssociation(
  entityId: string,
  associationId: string,
  ifMatchEtag: string,
  options: DiscoveryApiOptions = {},
): Promise<void> {
  const url = `${baseUrlFor(options)}/entities/${entityId}/associations/${associationId}`;
  const headers = buildHeaders(options);
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(
    url,
    makeFetchInit(options, headers, "entities.associations.remove", { method: "DELETE" }),
  );
  if (response.status === 204 || response.status === 200) {
    return;
  }
  throw new DiscoveryApiError(
    `DELETE ${url} → ${response.status}`,
    response.status,
    await readBodySafe(response),
  );
}
