import { useState } from "react";
import { formatPlan, type CurrentUser } from "../api";
import type { Navigate } from "../App";
import Overview from "./dashboard/Overview";
import KnowledgeBase from "./dashboard/KnowledgeBase";
import Conversations from "./dashboard/Conversations";
import WidgetSettings from "./dashboard/WidgetSettings";
import Billing from "./dashboard/Billing";

export type DashboardSection =
  | "overview"
  | "knowledge"
  | "conversations"
  | "widget"
  | "billing";

interface DashboardProps {
  user: CurrentUser;
  navigate: Navigate;
  onLogout: () => void;
}

const NAV_ITEMS: Array<{ key: DashboardSection; label: string }> = [
  { key: "overview", label: "Overview" },
  { key: "knowledge", label: "Knowledge Base" },
  { key: "conversations", label: "Conversations" },
  { key: "widget", label: "Widget" },
  { key: "billing", label: "Billing" },
];

export default function Dashboard({ user, navigate, onLogout }: DashboardProps) {
  const [section, setSection] = useState<DashboardSection>("overview");

  return (
    <div className="dashboard">
      <aside className="sidebar">
        <a
          className="logo"
          href="/"
          onClick={(e) => {
            e.preventDefault();
            navigate("/");
          }}
        >
          <span className="logo-mark">✈</span> SupportPilot AI
        </a>
        <div className="business">
          <div className="name">{user.businessName}</div>
          <div className="email">{user.email}</div>
          <span className="plan-badge" style={{ marginTop: "0.5rem" }}>
            {formatPlan(user.plan)} plan
          </span>
        </div>
        <nav>
          {NAV_ITEMS.map((item) => (
            <button
              key={item.key}
              className={`nav-item${section === item.key ? " active" : ""}`}
              onClick={() => setSection(item.key)}
            >
              {item.label}
            </button>
          ))}
        </nav>
        <button className="nav-item" onClick={onLogout}>
          Log out
        </button>
      </aside>

      <main className="main">
        {section === "overview" && (
          <Overview user={user} goTo={setSection} />
        )}
        {section === "knowledge" && <KnowledgeBase />}
        {section === "conversations" && <Conversations />}
        {section === "widget" && <WidgetSettings />}
        {section === "billing" && <Billing user={user} />}
      </main>
    </div>
  );
}
