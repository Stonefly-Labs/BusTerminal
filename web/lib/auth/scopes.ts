export const API_SCOPE: string = process.env.NEXT_PUBLIC_API_SCOPE ?? "";

export interface ScopeRequest {
  readonly scopes: readonly string[];
}

export const API_SCOPE_REQUEST: ScopeRequest = { scopes: [API_SCOPE] };
