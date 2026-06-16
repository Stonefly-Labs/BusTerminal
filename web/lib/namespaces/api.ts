/**
 * Spec 008 / T050. Typed namespace API client.
 *
 * Composes `web/lib/http/client.ts` so every UI-originated request
 * carries `traceparent` per the brand-foundation W3C Trace Context mandate.
 * Strongly-typed via Zod schemas — every response is parsed before being
 * returned.
 *
 * RSC-safe: no React imports, no MSAL dance. RSC callers pass an explicit
 * `accessToken` via `options`; Client Component callers compose
 * `useApiToken` (spec 007 pattern) and pass the result in.
 */

import { resolveMockRolesHeaderValue } from "@/lib/api-client";
import { httpFetch, type HttpFetchOptions } from "@/lib/http/client";
import { E2E_MOCK_ROLES_HEADER } from "@/tests/auth/personas";
import type { NamespaceInventoryFilter } from "./query-keys";
import {
  inventoryPageSchema,
  namespaceDetailsResponseSchema,
  onboardedNamespaceSchema,
  principalPickerItemSchema,
  validationRunListSchema,
  validationRunSchema,
  workloadIdentityResponseSchema,
  type InventoryPage,
  type LifecycleTransitionRequest,
  type NamespaceDetailsResponse,
  type OnboardedNamespace,
  type OnboardingRequest,
  type PrincipalPickerItem,
  type UpdateMetadataRequest,
  type UpdateOwnershipRequest,
  type ValidationRun,
  type ValidationRunList,
  type WorkloadIdentityResponse,
} from "./schemas";

const DEFAULT_BASE_URL = "/api/namespaces";

export interface NamespacesApiOptions {
  readonly accessToken?: string;
  readonly signal?: AbortSignal;
  readonly baseUrl?: string;
}

export class NamespacesApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body?: unknown,
  ) {
    super(message);
    this.name = "NamespacesApiError";
  }
}

function buildHeaders(options: NamespacesApiOptions, init?: HeadersInit): Headers {
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

function baseUrlFor(options: NamespacesApiOptions): string {
  return options.baseUrl ?? DEFAULT_BASE_URL;
}

function makeFetchInit(
  options: NamespacesApiOptions,
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

// === GET /api/namespaces/identity ===

export async function getIdentity(
  options: NamespacesApiOptions = {},
): Promise<WorkloadIdentityResponse> {
  const url = `${baseUrlFor(options)}/identity`;
  const response = await httpFetch(url, makeFetchInit(options, buildHeaders(options), "namespaces.identity"));
  if (!response.ok) {
    throw new NamespacesApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return workloadIdentityResponseSchema.parse(await readJsonOrThrow(response));
}

// === GET /api/namespaces/_picker ===

export async function searchPrincipals(
  query: string,
  options: NamespacesApiOptions & { includeGroups?: boolean; top?: number } = {},
): Promise<PrincipalPickerItem[]> {
  const search = new URLSearchParams({ q: query });
  if (options.includeGroups !== undefined) search.set("includeGroups", String(options.includeGroups));
  if (options.top !== undefined) search.set("top", String(options.top));
  const url = `${baseUrlFor(options)}/_picker?${search.toString()}`;
  const response = await httpFetch(url, makeFetchInit(options, buildHeaders(options), "namespaces.picker"));
  if (!response.ok) {
    throw new NamespacesApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const body = await readJsonOrThrow(response);
  // Contract returns { items: [...] }; tolerate a bare-array shape in case
  // a stub backend or test fixture inverts the wrapping.
  const items = Array.isArray(body)
    ? body
    : ((body as { items?: unknown[] } | undefined)?.items ?? []);
  return principalPickerItemSchema.array().parse(items);
}

// === POST /api/namespaces/_validate ===

export interface PreValidationParams {
  readonly proposedNamespaceId?: string;
  readonly azureResourceId: string;
}

export async function runPreOnboardingValidation(
  params: PreValidationParams,
  options: NamespacesApiOptions = {},
): Promise<ValidationRun> {
  const url = `${baseUrlFor(options)}/_validate`;
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  const response = await httpFetch(url, makeFetchInit(options, headers, "namespaces.validate", {
    method: "POST",
    body: JSON.stringify(params),
  }));
  if (!response.ok) {
    throw new NamespacesApiError(`POST ${url} → ${response.status}`, response.status, await safeBody(response));
  }
  return validationRunSchema.parse(await readJsonOrThrow(response));
}

// === POST /api/namespaces (register) ===

export async function register(
  body: OnboardingRequest,
  options: NamespacesApiOptions = {},
): Promise<OnboardedNamespace> {
  const url = baseUrlFor(options);
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  const response = await httpFetch(url, makeFetchInit(options, headers, "namespaces.register", {
    method: "POST",
    body: JSON.stringify(body),
  }));
  if (!response.ok) {
    throw new NamespacesApiError(`POST ${url} → ${response.status}`, response.status, await safeBody(response));
  }
  return onboardedNamespaceSchema.parse(await readJsonOrThrow(response));
}

// === GET /api/namespaces (inventory) ===

export async function listInventory(
  filter: NamespaceInventoryFilter & {
    pageSize?: number;
    continuationToken?: string;
    sort?: string;
  } = {},
  options: NamespacesApiOptions = {},
): Promise<InventoryPage> {
  const search = new URLSearchParams();
  if (filter.environment) search.set("environment", filter.environment);
  if (filter.lifecycleStatus) search.set("lifecycleStatus", filter.lifecycleStatus);
  if (filter.validationStatus) search.set("validationStatus", filter.validationStatus);
  if (filter.includeArchived !== undefined) search.set("includeArchived", String(filter.includeArchived));
  if (filter.q) search.set("q", filter.q);
  if (filter.tagKey) search.set("tagKey", filter.tagKey);
  if (filter.tagValue) search.set("tagValue", filter.tagValue);
  if (filter.sort) search.set("sort", filter.sort);
  if (filter.pageSize) search.set("pageSize", String(filter.pageSize));
  if (filter.continuationToken) search.set("continuationToken", filter.continuationToken);
  const qs = search.toString();
  const url = qs ? `${baseUrlFor(options)}?${qs}` : baseUrlFor(options);
  const response = await httpFetch(url, makeFetchInit(options, buildHeaders(options), "namespaces.inventory"));
  if (!response.ok) {
    throw new NamespacesApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return inventoryPageSchema.parse(await readJsonOrThrow(response));
}

// === GET /api/namespaces/{id} ===

export interface NamespaceWithEtag {
  readonly namespace: OnboardedNamespace;
  readonly etag: string;
}

export interface NamespaceDetailsWithEtag {
  readonly details: NamespaceDetailsResponse;
  readonly etag: string;
}

export async function getDetails(
  id: string,
  options: NamespacesApiOptions = {},
): Promise<NamespaceDetailsWithEtag | null> {
  const url = `${baseUrlFor(options)}/${id}`;
  const response = await httpFetch(url, makeFetchInit(options, buildHeaders(options), "namespaces.details"));
  if (response.status === 404) return null;
  if (!response.ok) {
    throw new NamespacesApiError(`GET ${url} → ${response.status}`, response.status);
  }
  const details = namespaceDetailsResponseSchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? details.id;
  return { details, etag };
}

// === PUT /api/namespaces/{id}/metadata ===

export async function updateMetadata(
  body: UpdateMetadataRequest,
  ifMatchEtag: string,
  options: NamespacesApiOptions = {},
): Promise<NamespaceWithEtag> {
  return putAndParse(`${baseUrlFor(options)}/${body.id}/metadata`, body, ifMatchEtag, options, "namespaces.metadata");
}

// === PUT /api/namespaces/{id}/ownership ===

export async function updateOwnership(
  body: UpdateOwnershipRequest,
  ifMatchEtag: string,
  options: NamespacesApiOptions = {},
): Promise<NamespaceWithEtag> {
  return putAndParse(`${baseUrlFor(options)}/${body.id}/ownership`, body, ifMatchEtag, options, "namespaces.ownership");
}

// === POST /api/namespaces/{id}/lifecycle ===

export async function transitionLifecycle(
  body: LifecycleTransitionRequest,
  ifMatchEtag: string,
  options: NamespacesApiOptions = {},
): Promise<NamespaceWithEtag> {
  const url = `${baseUrlFor(options)}/${body.id}/lifecycle`;
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(url, makeFetchInit(options, headers, "namespaces.lifecycle", {
    method: "POST",
    body: JSON.stringify(body),
  }));
  if (!response.ok) {
    throw new NamespacesApiError(`POST ${url} → ${response.status}`, response.status, await safeBody(response));
  }
  const ns = onboardedNamespaceSchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? ns.id;
  return { namespace: ns, etag };
}

// === POST /api/namespaces/{id}/validation-runs (re-run validation) ===

export async function runValidation(
  namespaceId: string,
  options: NamespacesApiOptions = {},
): Promise<ValidationRun> {
  const url = `${baseUrlFor(options)}/${namespaceId}/validation-runs`;
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  const response = await httpFetch(url, makeFetchInit(options, headers, "namespaces.revalidate", {
    method: "POST",
    body: JSON.stringify({}),
  }));
  if (!response.ok) {
    throw new NamespacesApiError(`POST ${url} → ${response.status}`, response.status, await safeBody(response));
  }
  return validationRunSchema.parse(await readJsonOrThrow(response));
}

// === GET /api/namespaces/{id}/validation-runs ===

export async function listValidationRuns(
  namespaceId: string,
  options: NamespacesApiOptions & { limit?: number; continuationToken?: string } = {},
): Promise<ValidationRunList> {
  const search = new URLSearchParams();
  if (options.limit) search.set("limit", String(options.limit));
  if (options.continuationToken) search.set("continuationToken", options.continuationToken);
  const qs = search.toString();
  const url = qs
    ? `${baseUrlFor(options)}/${namespaceId}/validation-runs?${qs}`
    : `${baseUrlFor(options)}/${namespaceId}/validation-runs`;
  const response = await httpFetch(url, makeFetchInit(options, buildHeaders(options), "namespaces.validation-runs.list"));
  if (!response.ok) {
    throw new NamespacesApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return validationRunListSchema.parse(await readJsonOrThrow(response));
}

// === GET /api/namespaces/{id}/validation-runs/{runId} ===

export async function getValidationRun(
  namespaceId: string,
  runId: string,
  options: NamespacesApiOptions = {},
): Promise<ValidationRun | null> {
  const url = `${baseUrlFor(options)}/${namespaceId}/validation-runs/${runId}`;
  const response = await httpFetch(url, makeFetchInit(options, buildHeaders(options), "namespaces.validation-runs.get"));
  if (response.status === 404) return null;
  if (!response.ok) {
    throw new NamespacesApiError(`GET ${url} → ${response.status}`, response.status);
  }
  return validationRunSchema.parse(await readJsonOrThrow(response));
}

// === helpers ===

async function putAndParse(
  url: string,
  body: unknown,
  ifMatchEtag: string,
  options: NamespacesApiOptions,
  operation: string,
): Promise<NamespaceWithEtag> {
  const headers = buildHeaders(options);
  headers.set("content-type", "application/json");
  headers.set("if-match", ifMatchEtag);
  const response = await httpFetch(url, makeFetchInit(options, headers, operation, {
    method: "PUT",
    body: JSON.stringify(body),
  }));
  if (!response.ok) {
    throw new NamespacesApiError(`PUT ${url} → ${response.status}`, response.status, await safeBody(response));
  }
  const ns = onboardedNamespaceSchema.parse(await readJsonOrThrow(response));
  const etag = response.headers.get("etag") ?? ns.id;
  return { namespace: ns, etag };
}

async function safeBody(response: Response): Promise<unknown> {
  try {
    return await readJsonOrThrow(response);
  } catch {
    return undefined;
  }
}
