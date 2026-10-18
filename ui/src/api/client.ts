export interface Credentials {
  token: string;
}

export interface AgentProfile {
  id: string;
  teamId: string | null;
  permissions: string[];
}

export interface AgentView {
  id: string;
  name: string;
  enabled: boolean;
}

export interface RegistryItem {
  type: "skill" | "tool";
  id: string;
  title: string;
  content: string;
}

export interface Proposal {
  id: string;
  path: string;
  operation: string;
  status: string;
  createdAt: string;
}

export interface MetadataIssue {
  path: string;
  issues: { code: string; field: string | null; message: string }[];
  checkedAt: string;
}

export interface AuditEvent {
  action: string;
  timestamp: string;
  result: string;
  metadata?: unknown;
}

export interface TraceView {
  traceId: string;
  rootAction: string;
  startedAt: string;
  status: string;
  durationMs: number | null;
}

export type ResourceResult<T> =
  | { status: "ready"; value: T }
  | { status: "unavailable"; reason: string };

export interface DashboardData {
  api: ResourceResult<{ status: string }>;
  agents: ResourceResult<AgentView[]>;
  skills: ResourceResult<RegistryItem[]>;
  tools: ResourceResult<RegistryItem[]>;
  proposals: ResourceResult<Proposal[]>;
  metadata: ResourceResult<MetadataIssue[]>;
  audit: ResourceResult<AuditEvent[]>;
  traces: ResourceResult<TraceView[]>;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message = `Brain API request failed (${status}).`,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

const apiBaseUrl = (import.meta.env.VITE_BRAIN_API_URL ?? "").replace(/\/+$/, "");

export async function request<T>(
  path: string,
  credentials: Credentials,
  signal?: AbortSignal,
  options?: Pick<RequestInit, "method" | "body">,
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers: {
      Accept: "application/json",
      ...(options?.body ? { "Content-Type": "application/json" } : {}),
      Authorization: `Bearer ${credentials.token}`,
    },
    signal,
  });

  if (!response.ok) {
    throw new ApiError(response.status);
  }

  return (await response.json()) as T;
}

export function authenticate(
  credentials: Credentials,
  signal?: AbortSignal,
): Promise<AgentProfile> {
  return request<AgentProfile>("/api/agents/me", credentials, signal);
}

export async function loadDashboard(
  credentials: Credentials,
  signal?: AbortSignal,
): Promise<DashboardData> {
  const endpoints = await Promise.all([
    settled(request<{ status: string }>("/health/ready", credentials, signal)),
    settled(request<AgentView[]>("/api/agents", credentials, signal)),
    settled(request<RegistryItem[]>("/api/skills", credentials, signal)),
    settled(request<RegistryItem[]>("/api/tools", credentials, signal)),
    settled(
      request<Proposal[]>(
        "/api/knowledge/proposals?status=pending&limit=100",
        credentials,
        signal,
      ),
    ),
    settled(
      request<MetadataIssue[]>(
        "/api/knowledge/metadata/issues?limit=1000",
        credentials,
        signal,
      ),
    ),
    settled(request<AuditEvent[]>("/api/audit?limit=8", credentials, signal)),
    settled(request<TraceView[]>("/api/traces?limit=8", credentials, signal)),
  ]);

  if (endpoints.some((result) => result.status === "rejected" && isUnauthorized(result.reason))) {
    throw new ApiError(401, "Your credentials are no longer valid.");
  }

  return {
    api: asResource(endpoints[0]),
    agents: asResource(endpoints[1]),
    skills: asResource(endpoints[2]),
    tools: asResource(endpoints[3]),
    proposals: asResource(endpoints[4]),
    metadata: asResource(endpoints[5]),
    audit: asResource(endpoints[6]),
    traces: asResource(endpoints[7]),
  };
}

async function settled<T>(promise: Promise<T>): Promise<PromiseSettledResult<T>> {
  return Promise.allSettled([promise]).then(([result]) => result);
}

function asResource<T>(result: PromiseSettledResult<T>): ResourceResult<T> {
  if (result.status === "fulfilled") {
    return { status: "ready", value: result.value };
  }

  const error = result.reason;
  if (error instanceof ApiError && error.status === 403) {
    return { status: "unavailable", reason: "Permission required" };
  }
  if (error instanceof ApiError && error.status >= 500) {
    return { status: "unavailable", reason: "Brain service unavailable" };
  }
  return { status: "unavailable", reason: "Could not load this data" };
}

function isUnauthorized(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}
