import { useEffect, useState } from "react";
import {
  api,
  formatPlan,
  type CurrentUser,
  type KnowledgeStats,
} from "../../api";
import type { DashboardSection } from "../Dashboard";

interface OverviewProps {
  user: CurrentUser;
  goTo: (section: DashboardSection) => void;
}

function usagePercent(used: number, limit: number): number {
  if (limit <= 0) return 0;
  return Math.min(100, Math.round((used / limit) * 100));
}

export default function Overview({ user, goTo }: OverviewProps) {
  const [stats, setStats] = useState<KnowledgeStats | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .knowledgeStats()
      .then((data) => {
        if (!cancelled) setStats(data);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Could not load your usage.",
          );
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div>
      <h1>Overview</h1>
      <p className="page-sub">
        Welcome back, {user.businessName}. Here is how your support agent is
        doing this month.
      </p>

      {error && <div className="notice notice-error">{error}</div>}

      <div className="stat-grid">
        <div className="stat-card">
          <div className="label">Current plan</div>
          <div className="value">{formatPlan(user.plan)}</div>
          <button
            className="btn btn-secondary btn-sm"
            onClick={() => goTo("billing")}
          >
            View plans
          </button>
        </div>

        <div className="stat-card">
          <div className="label">Chats this month</div>
          <div className="value">
            {stats ? stats.chatsThisMonth.toLocaleString() : "—"}
            {stats && (
              <span style={{ color: "var(--muted)", fontSize: "1rem" }}>
                {" "}
                / {stats.chatLimit.toLocaleString()}
              </span>
            )}
          </div>
          {stats && (
            <div className="progress">
              <div
                style={{
                  width: `${usagePercent(stats.chatsThisMonth, stats.chatLimit)}%`,
                }}
              />
            </div>
          )}
        </div>

        <div className="stat-card">
          <div className="label">Documents</div>
          <div className="value">
            {stats ? stats.documents.toLocaleString() : "—"}
            {stats && (
              <span style={{ color: "var(--muted)", fontSize: "1rem" }}>
                {" "}
                / {stats.documentLimit.toLocaleString()}
              </span>
            )}
          </div>
          {stats && (
            <div className="progress">
              <div
                style={{
                  width: `${usagePercent(stats.documents, stats.documentLimit)}%`,
                }}
              />
            </div>
          )}
        </div>

        <div className="stat-card">
          <div className="label">Indexed chunks</div>
          <div className="value">
            {stats ? stats.chunks.toLocaleString() : "—"}
          </div>
          <span style={{ color: "var(--muted)", fontSize: "0.9rem" }}>
            Searchable pieces of your docs
          </span>
        </div>
      </div>

      <div className="card">
        <h3>Next steps</h3>
        <p style={{ color: "var(--muted)" }}>
          Add your help docs in the Knowledge Base, style the widget, then
          paste the embed snippet on your website. Your agent answers from
          your docs — with sources — from that moment on.
        </p>
        <div className="toolbar" style={{ marginBottom: 0 }}>
          <button
            className="btn btn-primary"
            onClick={() => goTo("knowledge")}
          >
            Add documents
          </button>
          <button className="btn btn-secondary" onClick={() => goTo("widget")}>
            Get the embed snippet
          </button>
          <a className="btn btn-secondary" href="/demo.html">
            See the demo store
          </a>
        </div>
      </div>
    </div>
  );
}
