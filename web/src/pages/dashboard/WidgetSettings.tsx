import { useEffect, useState, type FormEvent } from "react";
import { api, type WidgetSettings as WidgetSettingsData } from "../../api";

function embedSnippet(widgetKey: string): string {
  const origin = window.location.origin;
  return `<script src="${origin}/widget/supportpilot-widget.js" data-widget-key="${widgetKey}" async></script>`;
}

async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

export default function WidgetSettings() {
  const [settings, setSettings] = useState<WidgetSettingsData | null>(null);
  const [color, setColor] = useState("#4f46e5");
  const [greeting, setGreeting] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .getWidgetSettings()
      .then((data) => {
        if (cancelled) return;
        setSettings(data);
        setColor(data.color || "#4f46e5");
        setGreeting(data.greeting || "");
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Could not load widget settings.",
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

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      const updated = await api.updateWidgetSettings(color, greeting);
      setSettings(updated);
      setNotice("Widget settings saved. The change is live on your site.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save settings.");
    } finally {
      setSaving(false);
    }
  }

  async function handleRegenerate() {
    if (
      !window.confirm(
        "Regenerate your widget key? The embed snippet on your website will stop working until you replace it with the new key.",
      )
    ) {
      return;
    }
    setRegenerating(true);
    setError(null);
    setNotice(null);
    try {
      const result = await api.regenerateWidgetKey();
      setSettings((prev) => (prev ? { ...prev, widgetKey: result.widgetKey } : prev));
      setNotice(
        "New widget key generated. Update the embed snippet on your website now — the old key no longer works.",
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not regenerate the key.");
    } finally {
      setRegenerating(false);
    }
  }

  async function handleCopy(label: string, text: string) {
    const ok = await copyToClipboard(text);
    setCopied(ok ? label : null);
    if (!ok) {
      setError("Copy failed — select the text and copy it manually.");
    }
  }

  if (loading) {
    return (
      <div>
        <h1>Widget</h1>
        <p className="empty">Loading widget settings…</p>
      </div>
    );
  }

  return (
    <div>
      <h1>Widget</h1>
      <p className="page-sub">
        Style the chat widget customers see on your website, then paste the
        embed snippet into your site.
      </p>

      {error && <div className="notice notice-error">{error}</div>}
      {notice && <div className="notice notice-success">{notice}</div>}

      <div className="card form-card">
        <h3>Appearance</h3>
        <form onSubmit={handleSave}>
          <div className="field">
            <label htmlFor="widget-color">Brand color</label>
            <div className="color-row">
              <input
                id="widget-color"
                type="color"
                value={color}
                onChange={(e) => setColor(e.target.value)}
              />
              <input
                type="text"
                aria-label="Brand color (hex)"
                value={color}
                onChange={(e) => setColor(e.target.value)}
                placeholder="#4f46e5"
              />
            </div>
            <p className="hint">
              Used for the chat bubble and the widget header on your site.
            </p>
          </div>
          <div className="field">
            <label htmlFor="widget-greeting">Greeting message</label>
            <textarea
              id="widget-greeting"
              rows={3}
              value={greeting}
              onChange={(e) => setGreeting(e.target.value)}
              placeholder="Hi! Ask me anything about our products, shipping, or returns."
            />
            <p className="hint">
              The first message customers see when they open the chat.
            </p>
          </div>
          <button className="btn btn-primary" type="submit" disabled={saving}>
            {saving ? "Saving…" : "Save widget settings"}
          </button>
        </form>
      </div>

      {settings && (
        <div className="card">
          <h3>Embed on your website</h3>
          <p style={{ color: "var(--muted)" }}>
            Your widget key identifies {settings.businessName} to the widget.
            Paste this one line just before <code>&lt;/body&gt;</code> on any
            page where customers should be able to chat:
          </p>

          <div className="field">
            <label>Widget key</label>
            <div className="copy-row">
              <code>{settings.widgetKey}</code>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => void handleCopy("key", settings.widgetKey)}
              >
                {copied === "key" ? "Copied ✓" : "Copy"}
              </button>
            </div>
          </div>

          <div className="snippet">
            <pre>{embedSnippet(settings.widgetKey)}</pre>
          </div>
          <div className="toolbar">
            <button
              className="btn btn-secondary"
              onClick={() =>
                void handleCopy("snippet", embedSnippet(settings.widgetKey))
              }
            >
              {copied === "snippet" ? "Copied ✓" : "Copy embed snippet"}
            </button>
            <span className="spacer" />
            <button
              className="btn btn-danger"
              onClick={() => void handleRegenerate()}
              disabled={regenerating}
            >
              {regenerating ? "Regenerating…" : "Regenerate key"}
            </button>
          </div>
          <p className="hint" style={{ color: "var(--muted)", fontSize: "0.85rem" }}>
            ⚠️ Regenerating the key breaks every existing embed until you
            update your website with the new snippet.
          </p>
        </div>
      )}
    </div>
  );
}
