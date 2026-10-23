import type { CurrentUser } from "../api";
import type { Navigate } from "../App";

interface LandingProps {
  navigate: Navigate;
  user: CurrentUser | null;
}

const FEATURES: Array<{ icon: string; title: string; body: string }> = [
  {
    icon: "📚",
    title: "Train on your docs",
    body: "Paste text, add Q&A pairs, or upload .txt and .md files. SupportPilot chunks and indexes everything so answers come from your content — not the open internet.",
  },
  {
    icon: "🔎",
    title: "Answers with sources",
    body: "Every answer cites the documents it came from, so customers can trust it and you can audit exactly what the agent said and why.",
  },
  {
    icon: "🏷️",
    title: "One script tag",
    body: "Drop a single <script> tag on your site and the chat widget appears — in your brand color, with your greeting. No build step, no iframe hacks.",
  },
  {
    icon: "🔌",
    title: "Demo mode or your OpenAI key",
    body: "Try it instantly in offline demo mode, or plug in your own OpenAI key for full AI answers. Either way your docs and data stay yours.",
  },
  {
    icon: "📊",
    title: "Plans & quotas",
    body: "Simple plans with clear document and monthly chat limits. Usage is tracked for you, so there are never surprise bills.",
  },
  {
    icon: "💬",
    title: "Conversation history",
    body: "Read every conversation the agent has had, with citations and customer 👍 / 👎 feedback, and improve your docs where it falls short.",
  },
];

const PLANS: Array<{
  name: string;
  price: string;
  per: string;
  features: string[];
  featured?: boolean;
}> = [
  {
    name: "Free",
    price: "$0",
    per: "forever",
    features: ["3 documents", "100 chats / month", "Widget with your branding", "Conversation history"],
  },
  {
    name: "Starter",
    price: "$29",
    per: "per month",
    features: ["50 documents", "2,000 chats / month", "Widget with your branding", "Conversation history", "Email support"],
    featured: true,
  },
  {
    name: "Pro",
    price: "$99",
    per: "per month",
    features: ["500 documents", "20,000 chats / month", "Widget with your branding", "Conversation history", "Priority support"],
  },
];

export default function Landing({ navigate, user }: LandingProps) {
  return (
    <div>
      <header className="site-nav">
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
        <nav className="nav-links">
          <a href="/demo.html">Live demo</a>
          {user ? (
            <button
              className="btn btn-primary"
              onClick={() => navigate("/dashboard")}
            >
              Go to dashboard
            </button>
          ) : (
            <>
              <a
                href="/login"
                onClick={(e) => {
                  e.preventDefault();
                  navigate("/login");
                }}
              >
                Log in
              </a>
              <button
                className="btn btn-primary"
                onClick={() => navigate("/register")}
              >
                Get started free
              </button>
            </>
          )}
        </nav>
      </header>

      <section className="hero">
        <h1>
          An AI support agent for <span className="accent">small businesses</span>
        </h1>
        <p className="lede">
          Train it on your own docs in minutes, drop one script tag on your
          website, and it answers customer questions 24/7 — with sources.
        </p>
        <div className="hero-actions">
          <button
            className="btn btn-primary btn-lg"
            onClick={() => navigate(user ? "/dashboard" : "/register")}
          >
            {user ? "Open your dashboard" : "Start free"}
          </button>
          <a className="btn btn-secondary btn-lg" href="/demo.html">
            Try the live demo
          </a>
        </div>
      </section>

      <section className="section">
        <h2>Everything a small team needs</h2>
        <p className="section-sub">
          No enterprise sales call, no machine-learning team. Just your docs
          and a widget.
        </p>
        <div className="feature-grid">
          {FEATURES.map((feature) => (
            <div className="feature" key={feature.title}>
              <div className="icon" aria-hidden="true">
                {feature.icon}
              </div>
              <h3>{feature.title}</h3>
              <p>{feature.body}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="section">
        <h2>How it works</h2>
        <p className="section-sub">Live on your website in about five minutes.</p>
        <div className="steps">
          <div className="step">
            <div className="num">1</div>
            <h3>Add your docs</h3>
            <p>
              Paste your help articles, policies, and FAQs — or upload files —
              and SupportPilot indexes them automatically.
            </p>
          </div>
          <div className="step">
            <div className="num">2</div>
            <h3>Style your widget</h3>
            <p>
              Pick your brand color and write a greeting. Copy the one-line
              embed snippet we generate for you.
            </p>
          </div>
          <div className="step">
            <div className="num">3</div>
            <h3>Paste one script tag</h3>
            <p>
              Add the snippet to your site and the agent starts answering
              customer questions around the clock, with sources.
            </p>
          </div>
        </div>
      </section>

      <section className="section">
        <h2>Simple pricing</h2>
        <p className="section-sub">Start free. Upgrade only when you outgrow it.</p>
        <div className="pricing-grid">
          {PLANS.map((plan) => (
            <div
              className={`price-card${plan.featured ? " featured" : ""}`}
              key={plan.name}
            >
              {plan.featured && <span className="plan-badge">Most popular</span>}
              <h3>{plan.name}</h3>
              <div>
                <span className="price">{plan.price}</span>{" "}
                <span className="per">{plan.per}</span>
              </div>
              <ul>
                {plan.features.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
              <button
                className={`btn ${plan.featured ? "btn-primary" : "btn-secondary"}`}
                onClick={() => navigate(user ? "/dashboard" : "/register")}
              >
                {plan.name === "Free" ? "Start free" : `Choose ${plan.name}`}
              </button>
            </div>
          ))}
        </div>
      </section>

      <section className="section">
        <div className="demo-callout">
          <h2>See it answer real questions</h2>
          <p>
            Our demo storefront, Acme Outdoors, runs the real SupportPilot
            widget. Ask it about tents, shipping, or returns — every answer
            comes from the store&apos;s own docs.
          </p>
          <a className="btn btn-lg" href="/demo.html">
            Open the live demo →
          </a>
        </div>
      </section>

      <footer className="site-footer">
        SupportPilot AI — an AI support agent for small businesses. Train it on
        your docs, embed it with one script tag, answer customers 24/7.
      </footer>
    </div>
  );
}
