import { useEffect, useState } from "react";
import {
  api,
  formatDate,
  type ConversationDetail,
  type ConversationSummary,
  type FeedbackValue,
} from "../../api";

/** Normalise the backend's feedback value into something displayable. */
function feedbackLabel(feedback: FeedbackValue): string | null {
  if (feedback === null || feedback === undefined || feedback === "") {
    return null;
  }
  if (feedback === true || feedback === 1) return "👍 Helpful";
  if (feedback === false || feedback === -1) return "👎 Not helpful";
  const text = String(feedback).toLowerCase();
  if (["up", "positive", "helpful", "like", "liked", "thumb_up", "thumbs_up"].includes(text)) {
    return "👍 Helpful";
  }
  if (
    ["down", "negative", "unhelpful", "dislike", "disliked", "thumb_down", "thumbs_down"].includes(text)
  ) {
    return "👎 Not helpful";
  }
  return `Feedback: ${String(feedback)}`;
}

export default function Conversations() {
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ConversationDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .listConversations()
      .then((data) => {
        if (!cancelled) setConversations(data);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Could not load conversations.",
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

  async function openConversation(id: string) {
    setSelectedId(id);
    setDetail(null);
    setDetailLoading(true);
    setError(null);
    try {
      setDetail(await api.getConversation(id));
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Could not load that conversation.",
      );
    } finally {
      setDetailLoading(false);
    }
  }

  return (
    <div>
      <h1>Conversations</h1>
      <p className="page-sub">
        Every chat your agent has had, with the sources behind each answer and
        the customer&apos;s feedback.
      </p>

      {error && <div className="notice notice-error">{error}</div>}

      {loading ? (
        <p className="empty">Loading conversations…</p>
      ) : conversations.length === 0 ? (
        <p className="empty">
          No conversations yet. Once the widget is on your site, chats will
          appear here.
        </p>
      ) : (
        <div className="conv-layout">
          <div className="conv-list">
            {conversations.map((conv) => (
              <button
                key={conv.id}
                className={`conv-item${selectedId === conv.id ? " active" : ""}`}
                onClick={() => void openConversation(conv.id)}
              >
                <div>
                  <strong>{conv.messageCount} message(s)</strong>
                </div>
                <div className="meta">
                  Started {formatDate(conv.startedAt)}
                  <br />
                  Last message {formatDate(conv.lastMessageAt)}
                </div>
              </button>
            ))}
          </div>

          <div>
            {detailLoading && <p className="empty">Loading transcript…</p>}
            {!detailLoading && !detail && (
              <p className="empty">Select a conversation to read the full transcript.</p>
            )}
            {detail && (
              <div className="transcript">
                <p style={{ color: "var(--muted)", marginBottom: "1.25rem" }}>
                  Started {formatDate(detail.startedAt)} · Session{" "}
                  <code>{detail.sessionId}</code>
                </p>
                {detail.messages.map((message) => {
                  const isUser = message.role === "user";
                  const feedback = feedbackLabel(message.feedback);
                  return (
                    <div
                      key={message.id}
                      className={`msg ${isUser ? "msg-user" : "msg-assistant"}`}
                    >
                      <div>{message.content}</div>
                      {!isUser && message.citations.length > 0 && (
                        <div className="citations">
                          <strong>Sources:</strong>
                          {message.citations.map((citation, index) => (
                            <blockquote key={index}>
                              <strong>{citation.documentTitle}</strong>
                              {citation.snippet
                                ? ` — ${citation.snippet}`
                                : ""}
                            </blockquote>
                          ))}
                        </div>
                      )}
                      {!isUser && feedback && (
                        <div className="feedback">{feedback}</div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
