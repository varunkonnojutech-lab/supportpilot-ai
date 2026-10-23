import { useState, type FormEvent } from "react";
import { api, setToken, type CurrentUser } from "../api";
import type { Navigate } from "../App";

interface LoginProps {
  navigate: Navigate;
  onAuthed: (user: CurrentUser) => void;
}

export default function Login({ navigate, onAuthed }: LoginProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const auth = await api.login(email.trim(), password);
      setToken(auth.token);
      onAuthed({
        email: auth.email,
        businessName: auth.businessName,
        role: auth.role,
        plan: auth.plan,
      });
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Login failed. Please try again.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-wrap">
      <p style={{ textAlign: "center", marginBottom: "1.5rem" }}>
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
      </p>
      <div className="card">
        <h1 style={{ fontSize: "1.4rem" }}>Welcome back</h1>
        <p style={{ color: "var(--muted)" }}>
          Log in to manage your support agent.
        </p>
        {error && <div className="notice notice-error">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
          <button
            className="btn btn-primary"
            type="submit"
            disabled={submitting}
            style={{ width: "100%" }}
          >
            {submitting ? "Logging in…" : "Log in"}
          </button>
        </form>
      </div>
      <p className="auth-alt">
        New to SupportPilot AI?{" "}
        <a
          href="/register"
          onClick={(e) => {
            e.preventDefault();
            navigate("/register");
          }}
        >
          Create a free account
        </a>
      </p>
    </div>
  );
}
