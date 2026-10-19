import { AgentView, authenticate, Credentials, request } from "../../api/client";

export interface AgentDetails extends AgentView {
  description: string;
  teamId: string | null;
  roles: string[];
  directPermissions: string[];
  permissions: string[];
  createdAt: string;
  updatedAt: string;
}

export interface Role {
  id: string;
  description: string;
  permissions: string[];
}

export interface CreateRoleInput {
  id: string;
  description: string;
  permissions: string[];
}

export interface Permission {
  id: string;
  description: string;
}

export interface AccessInput {
  roles: string[];
  permissions: string[];
  enabled: boolean;
}

export interface CreateInput {
  id: string;
  name: string;
  description: string;
  teamId: string | null;
  roles: string[];
  permissions: string[];
}

export function loadAgent(credentials: Credentials, id: string, signal?: AbortSignal) {
  return request<AgentDetails>(`/api/agents/${encodeURIComponent(id)}/details`, credentials, signal);
}

export async function loadDirectory(credentials: Credentials, signal: AbortSignal) {
  const [agents, roles, permissions, identity] = await Promise.all([
    request<AgentDetails[]>("/api/agents", credentials, signal),
    request<Role[]>("/api/roles", credentials, signal),
    request<Permission[]>("/api/permissions", credentials, signal),
    authenticate(credentials, signal),
  ]);
  return { agents, roles, permissions, identity };
}

export function saveAgent(credentials: Credentials, input: CreateInput | AccessInput, id: string | null, signal: AbortSignal) {
  return request<AgentDetails>(id === null ? "/api/agents" : `/api/agents/${encodeURIComponent(id)}/access`,
    credentials, signal, { method: id === null ? "POST" : "PUT", body: JSON.stringify(input) });
}

export function createRole(credentials: Credentials, input: CreateRoleInput, signal: AbortSignal) {
  return request<Role>("/api/roles", credentials, signal, { method: "POST", body: JSON.stringify(input) });
}
