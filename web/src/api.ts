// Typed client for the SupportPilot AI API.
//
// Contract (see README / backend):
// - Authenticated endpoints live under "/api" and expect
//   `Authorization: Bearer <token>`; the token is kept in localStorage.
// - Public widget endpoints live under "/api/public" and need no auth.
// - Errors come back as JSON: { "error": "message" }.

const TOKEN_KEY = "supportpilot_token";
const API_BASE = "/api";

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AuthResponse {
  token: string;
  email: string;
  businessName: string;
  role: string;
  plan: string;
}

export interface CurrentUser {
  email: string;
  businessName: string;
  role: string;
  plan: string;
}

export interface KnowledgeDocument {
  id: string;
  title: string;
  type: string;
  content: string;
  chunkCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface KnowledgeStats {
  documents: number;
  chunks: number;
  documentLimit: number;
  chatsThisMonth: number;
  chatLimit: number;
}

export interface Citation {
  documentTitle: string;
  snippet: string;
}

/** Feedback as stored by the backend: tolerant union, normalised by the UI. */
export type FeedbackValue = string | number | boolean | null;

export interface ConversationMessage {
  id: string;
  role: string;
  content: string;
  citations: Citation[];
  feedback: FeedbackValue;
  createdAt: string;
}

export interface ConversationSummary {
  id: string;
  sessionId: string;
  startedAt: string;
  lastMessageAt: string;
  messageCount: number;
}

export interface ConversationDetail {
  id: string;
  sessionId: string;
  startedAt: string;
  messages: ConversationMessage[];
}

export interface WidgetSettings {
  widgetKey: string;
  color: string;
  greeting: string;
  businessName: string;
}

export interface BillingStatus {
  plan: string;
  configured: boolean;
}

// ---------------------------------------------------------------------------
// Low-level request helper
// ---------------------------------------------------------------------------

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers ?? {});

  const isFormData =
    typeof FormData !== "undefined" && init.body instanceof FormData;
  if (!isFormData && init.body !== undefined && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = getToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, { ...init, headers });
  } catch {
    throw new ApiError(
      "Could not reach the SupportPilot API. Is the backend running?",
      0,
    );
  }

  const text = await response.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (!response.ok) {
    const message =
      data !== null &&
      typeof data === "object" &&
      "error" in data &&
      typeof (data as { error: unknown }).error === "string"
        ? (data as { error: string }).error
        : `Request failed (${response.status}).`;
    if (response.status === 401) {
      clearToken();
    }
    throw new ApiError(message, response.status);
  }

  return data as T;
}

function jsonBody(value: unknown): string {
  return JSON.stringify(value);
}

// ---------------------------------------------------------------------------
// API surface
// ---------------------------------------------------------------------------

export const api = {
  // Auth ---------------------------------------------------------------------
  register(
    businessName: string,
    email: string,
    password: string,
  ): Promise<AuthResponse> {
    return request<AuthResponse>("/auth/register", {
      method: "POST",
      body: jsonBody({ businessName, email, password }),
    });
  },

  login(email: string, password: string): Promise<AuthResponse> {
    return request<AuthResponse>("/auth/login", {
      method: "POST",
      body: jsonBody({ email, password }),
    });
  },

  me(): Promise<CurrentUser> {
    return request<CurrentUser>("/auth/me");
  },

  // Knowledge base -----------------------------------------------------------
  listDocuments(): Promise<KnowledgeDocument[]> {
    return request<KnowledgeDocument[]>("/knowledge/documents");
  },

  createDocument(
    title: string,
    type: string,
    content: string,
  ): Promise<KnowledgeDocument> {
    return request<KnowledgeDocument>("/knowledge/documents", {
      method: "POST",
      body: jsonBody({ title, type, content }),
    });
  },

  updateDocument(
    id: string,
    title: string,
    type: string,
    content: string,
  ): Promise<KnowledgeDocument> {
    return request<KnowledgeDocument>(
      `/knowledge/documents/${encodeURIComponent(id)}`,
      {
        method: "PUT",
        body: jsonBody({ title, type, content }),
      },
    );
  },

  deleteDocument(id: string): Promise<unknown> {
    return request<unknown>(`/knowledge/documents/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
  },

  uploadDocument(file: File): Promise<KnowledgeDocument> {
    const form = new FormData();
    form.append("file", file);
    return request<KnowledgeDocument>("/knowledge/upload", {
      method: "POST",
      body: form,
    });
  },

  reindex(): Promise<{ documents: number; chunks: number }> {
    return request<{ documents: number; chunks: number }>(
      "/knowledge/reindex",
      { method: "POST" },
    );
  },

  knowledgeStats(): Promise<KnowledgeStats> {
    return request<KnowledgeStats>("/knowledge/stats");
  },

  // Conversations --------------------------------------------------------------
  listConversations(): Promise<ConversationSummary[]> {
    return request<ConversationSummary[]>("/conversations");
  },

  getConversation(id: string): Promise<ConversationDetail> {
    return request<ConversationDetail>(
      `/conversations/${encodeURIComponent(id)}`,
    );
  },

  // Widget settings --------------------------------------------------------------
  getWidgetSettings(): Promise<WidgetSettings> {
    return request<WidgetSettings>("/settings/widget");
  },

  updateWidgetSettings(color: string, greeting: string): Promise<WidgetSettings> {
    return request<WidgetSettings>("/settings/widget", {
      method: "PUT",
      body: jsonBody({ color, greeting }),
    });
  },

  regenerateWidgetKey(): Promise<{ widgetKey: string }> {
    return request<{ widgetKey: string }>("/settings/widget/regenerate-key", {
      method: "POST",
    });
  },

  // Billing ----------------------------------------------------------------------
  billingStatus(): Promise<BillingStatus> {
    return request<BillingStatus>("/billing/status");
  },

  createCheckout(plan: string): Promise<{ url: string }> {
    return request<{ url: string }>("/billing/checkout", {
      method: "POST",
      body: jsonBody({ plan }),
    });
  },

  /**
   * Assumption: the "Manage billing" portal is exposed as
   * POST /api/billing/portal -> { url }, mirroring the checkout endpoint.
   * The contract only names the checkout/status endpoints, so the Billing
   * page treats a failure here as a friendly notice rather than an error.
   */
  createBillingPortal(): Promise<{ url: string }> {
    return request<{ url: string }>("/billing/portal", {
      method: "POST",
    });
  },
};

// ---------------------------------------------------------------------------
// Small shared helpers
// ---------------------------------------------------------------------------

export function formatDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export function formatPlan(plan: string): string {
  if (!plan) return "Free";
  return plan.charAt(0).toUpperCase() + plan.slice(1).toLowerCase();
}
