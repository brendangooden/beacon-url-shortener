// Thin fetch client. Cookie-BFF ready (credentials: include) though the POC backend's Dev auth
// provider authenticates every request, so no token/login is needed yet.

import { getAccessToken } from "@/auth/entra";

const BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? "/api";
// Origin root (public branding lives here, not under /api).
const ROOT = BASE.replace(/\/api\/?$/, "");

export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
}

export class ApiError extends Error {
  readonly status: number;
  readonly detail?: string;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail || problem?.title || `Request failed (${status})`);
    this.name = "ApiError";
    this.status = status;
    this.detail = problem?.detail;
  }
}

async function authHeaders(base: HeadersInit): Promise<HeadersInit> {
  // Dev mode: getAccessToken() returns null and no header is added (backend Dev auth authenticates).
  const token = await getAccessToken();
  return token ? { ...base, Authorization: `Bearer ${token}` } : base;
}

async function handleError(res: Response): Promise<never> {
  // Note: do NOT auto-trigger interactive re-auth on 401. getAccessToken() proactively refreshes
  // before expiry, so a 401 here means a real auth error (not expiry) — re-auth would loop. The
  // "session gone" case is handled interactively inside getAccessToken (InteractionRequiredAuthError).
  let problem: ProblemDetails | undefined;
  try {
    problem = (await res.json()) as ProblemDetails;
  } catch {
    problem = undefined;
  }
  throw new ApiError(res.status, problem);
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = await authHeaders({ "Content-Type": "application/json", ...(init?.headers ?? {}) });
  const res = await fetch(`${BASE}${path}`, { ...init, credentials: "include", headers });

  if (!res.ok) {
    return handleError(res);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return (await res.json()) as T;
}

async function postForm<T>(path: string, form: FormData): Promise<T> {
  // No Content-Type header — the browser sets the multipart boundary.
  const headers = await authHeaders({});
  const res = await fetch(`${BASE}${path}`, { method: "POST", credentials: "include", headers, body: form });
  if (!res.ok) {
    return handleError(res);
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

async function getRoot<T>(path: string): Promise<T> {
  const res = await fetch(`${ROOT}${path}`, { credentials: "include" });
  if (!res.ok) {
    throw new ApiError(res.status);
  }
  return (await res.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  getRoot,
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body) }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PATCH", body: body === undefined ? undefined : JSON.stringify(body) }),
  del: <T>(path: string) => request<T>(path, { method: "DELETE" }),
  postForm,
};

/** Absolute URL for a root-origin asset path such as the branding logoUrl. */
export const rootUrl = (path: string) => `${ROOT}${path}`;

export const V1 = "/v1";
