import { useState, type FormEvent } from "react";
import { api, setToken, type CurrentUser } from "../api";
import type { Navigate } from "../App";

interface RegisterProps {
  navigate: Navigate;
  onAuthed: (user: CurrentUser) => void;
}

export default function Register({ navigate, onAuthed }: RegisterProps) {
  const [businessName, setBusinessName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const auth = await api.register(businessName.trim(), email.trim(), password);
      setToken(auth.token);
      onAuthed({
        email: auth.email,
        businessName: auth.businessName,
        role: auth.role,
        plan: auth.plan,
      });
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Registration failed. Please try again.",
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
        <h1 style={{ fontSize: "1.4rem" }}>Create your account</h1>
        <p style={{ color: "var(--muted)" }}>
          Free plan included — 3 documents and 100 chats a month, no card
          required.
        </p>
        {error && <div className="notice notice-error">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="register-business">Business name</label>
            <input
              id="register-business"
              type="text"
              autoComplete="organization"
              required
              value={businessName}
              onChange={(e) => setBusinessName(e.target.value)}
              placeholder="Acme Outdoors"
            />
          </div>
          <div className="field">
            <label htmlFor="register-email">Email</label>
            <input
              id="register-email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="register-password">Password</label>
            <input
              id="register-password"
              type="password"
              autoComplete="new-password"
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
            {submitting ? "Creating account…" : "Create free account"}
          </button>
        </form>
      </div>
      <p className="auth-alt">
        Already have an account?{" "}
        <a
          href="/login"
          onClick={(e) => {
            e.preventDefault();
            navigate("/login");
          }}
        >
          Log in
        </a>
      </p>
    </div>
  );
}
