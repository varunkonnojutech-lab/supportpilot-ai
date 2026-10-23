import { useEffect, useState } from "react";
import { api, formatPlan, type BillingStatus, type CurrentUser } from "../../api";

interface BillingProps {
  user: CurrentUser;
}

const PLAN_CARDS: Array<{
  key: string;
  name: string;
  price: string;
  features: string[];
}> = [
  {
    key: "free",
    name: "Free",
    price: "$0",
    features: ["3 documents", "100 chats / month"],
  },
  {
    key: "starter",
    name: "Starter",
    price: "$29/mo",
    features: ["50 documents", "2,000 chats / month"],
  },
  {
    key: "pro",
    name: "Pro",
    price: "$99/mo",
    features: ["500 documents", "20,000 chats / month"],
  },
];

function isCurrentPlan(cardKey: string, plan: string): boolean {
  return cardKey.toLowerCase() === plan.toLowerCase();
}

export default function Billing({ user }: BillingProps) {
  const [status, setStatus] = useState<BillingStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyPlan, setBusyPlan] = useState<string | null>(null);
  const [portalBusy, setPortalBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .billingStatus()
      .then((data) => {
        if (!cancelled) setStatus(data);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Could not load billing status.",
          );
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const currentPlan = status?.plan ?? user.plan;
  const configured = status?.configured ?? false;

  async function handleUpgrade(plan: string) {
    setBusyPlan(plan);
    setNotice(null);
    setError(null);
    try {
      const result = await api.createCheckout(plan);
      if (result.url) {
        window.location.href = result.url;
        return;
      }
      setNotice("Checkout did not return a payment link. Please try again.");
    } catch (err) {
      // When billing isn't configured the API returns an error JSON with a
      // friendly message — surface it as a notice, not a hard error.
      setNotice(
        err instanceof Error
          ? err.message
          : "Billing is not available right now.",
      );
    } finally {
      setBusyPlan(null);
    }
  }

  async function handlePortal() {
    setPortalBusy(true);
    setNotice(null);
    setError(null);
    try {
      const result = await api.createBillingPortal();
      if (result.url) {
        window.location.href = result.url;
        return;
      }
      setNotice("The billing portal did not return a link. Please try again.");
    } catch (err) {
      setNotice(
        err instanceof Error
          ? err.message
          : "The billing portal is not available right now.",
      );
    } finally {
      setPortalBusy(false);
    }
  }

  return (
    <div>
      <h1>Billing</h1>
      <p className="page-sub">
        You are on the <strong>{formatPlan(currentPlan)}</strong> plan.
      </p>

      {error && <div className="notice notice-error">{error}</div>}
      {notice && <div className="notice notice-info">{notice}</div>}

      {!loading && status && !configured && (
        <div className="notice notice-info">
          Online billing isn&apos;t configured on this deployment yet, so
          upgrades are handled manually. Your current plan and quotas still
          apply exactly as shown.
        </div>
      )}

      <div className="pricing-grid" style={{ marginBottom: "1.5rem" }}>
        {PLAN_CARDS.map((card) => {
          const current = isCurrentPlan(card.key, currentPlan);
          return (
            <div
              className={`price-card${current ? " featured" : ""}`}
              key={card.key}
            >
              {current && <span className="plan-badge">Current plan</span>}
              <h3>{card.name}</h3>
              <div>
                <span className="price">{card.price}</span>
              </div>
              <ul>
                {card.features.map((feature) => (
                  <li key={feature}>{feature}</li>
                ))}
              </ul>
              {current ? (
                <button className="btn btn-secondary" disabled>
                  Current plan
                </button>
              ) : card.key === "free" ? (
                <button className="btn btn-secondary" disabled>
                  Included by default
                </button>
              ) : (
                <button
                  className="btn btn-primary"
                  disabled={busyPlan !== null}
                  onClick={() => void handleUpgrade(card.key)}
                >
                  {busyPlan === card.key
                    ? "Opening checkout…"
                    : `Upgrade to ${card.name}`}
                </button>
              )}
            </div>
          );
        })}
      </div>

      {configured && (
        <div className="card">
          <h3>Manage billing</h3>
          <p style={{ color: "var(--muted)" }}>
            Update your payment method, download invoices, or change your
            plan in the billing portal.
          </p>
          <button
            className="btn btn-secondary"
            disabled={portalBusy}
            onClick={() => void handlePortal()}
          >
            {portalBusy ? "Opening portal…" : "Manage billing"}
          </button>
        </div>
      )}
    </div>
  );
}
