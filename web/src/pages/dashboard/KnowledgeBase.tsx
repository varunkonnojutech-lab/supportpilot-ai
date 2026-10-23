import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { api, formatDate, type KnowledgeDocument } from "../../api";

type DocType = "text" | "qa";

/** Split a stored Q&A document ("Q: ...\nA: ...") back into its two fields. */
function parseQa(content: string): { question: string; answer: string } {
  const match = content.match(/^Q:\s*([\s\S]*?)\nA:\s*([\s\S]*)$/);
  if (match) {
    return { question: match[1].trim(), answer: match[2].trim() };
  }
  return { question: "", answer: content };
}

interface DocFormState {
  title: string;
  type: DocType;
  content: string;
  question: string;
  answer: string;
}

const EMPTY_FORM: DocFormState = {
  title: "",
  type: "text",
  content: "",
  question: "",
  answer: "",
};

function formFromDocument(doc: KnowledgeDocument): DocFormState {
  if (doc.type === "qa") {
    const { question, answer } = parseQa(doc.content);
    return {
      title: doc.title,
      type: "qa",
      content: "",
      question,
      answer,
    };
  }
  return {
    title: doc.title,
    type: "text",
    content: doc.content,
    question: "",
    answer: "",
  };
}

function composeContent(form: DocFormState): string {
  if (form.type === "qa") {
    return `Q: ${form.question.trim()}\nA: ${form.answer.trim()}`;
  }
  return form.content;
}

export default function KnowledgeBase() {
  const [documents, setDocuments] = useState<KnowledgeDocument[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [form, setForm] = useState<DocFormState>(EMPTY_FORM);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [uploading, setUploading] = useState(false);
  const [reindexing, setReindexing] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setDocuments(await api.listDocuments());
      setError(null);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Could not load documents.",
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function resetForm() {
    setForm(EMPTY_FORM);
    setEditingId(null);
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      const content = composeContent(form);
      if (editingId) {
        await api.updateDocument(editingId, form.title.trim(), form.type, content);
        setNotice("Document updated.");
      } else {
        await api.createDocument(form.title.trim(), form.type, content);
        setNotice("Document added and indexed.");
      }
      resetForm();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the document.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(doc: KnowledgeDocument) {
    if (
      !window.confirm(
        `Delete "${doc.title}"? The agent will stop using it immediately.`,
      )
    ) {
      return;
    }
    setError(null);
    setNotice(null);
    try {
      await api.deleteDocument(doc.id);
      if (editingId === doc.id) resetForm();
      setNotice("Document deleted.");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not delete the document.");
    }
  }

  async function handleUpload(file: File | undefined) {
    if (!file) return;
    setUploading(true);
    setError(null);
    setNotice(null);
    try {
      await api.uploadDocument(file);
      setNotice(`Uploaded "${file.name}" and indexed it.`);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed.");
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  async function handleReindex() {
    setReindexing(true);
    setError(null);
    setNotice(null);
    try {
      const result = await api.reindex();
      setNotice(
        `Reindex complete: ${result.documents} document(s), ${result.chunks} chunk(s).`,
      );
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Reindex failed.");
    } finally {
      setReindexing(false);
    }
  }

  return (
    <div>
      <h1>Knowledge Base</h1>
      <p className="page-sub">
        These are the only sources your agent answers from. Add help articles,
        policies, and FAQs — every answer will cite them.
      </p>

      {error && <div className="notice notice-error">{error}</div>}
      {notice && <div className="notice notice-success">{notice}</div>}

      <div className="card form-card">
        <h3>{editingId ? "Edit document" : "Add a document"}</h3>
        <form onSubmit={handleSave}>
          <div className="field">
            <label htmlFor="doc-title">Title</label>
            <input
              id="doc-title"
              type="text"
              required
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              placeholder="Returns & refunds policy"
            />
          </div>
          <div className="field">
            <label htmlFor="doc-type">Type</label>
            <select
              id="doc-type"
              value={form.type}
              onChange={(e) =>
                setForm({ ...form, type: e.target.value as DocType })
              }
            >
              <option value="text">Text document</option>
              <option value="qa">Q&amp;A pair</option>
            </select>
          </div>

          {form.type === "qa" ? (
            <>
              <div className="field">
                <label htmlFor="doc-question">Question</label>
                <input
                  id="doc-question"
                  type="text"
                  required
                  value={form.question}
                  onChange={(e) => setForm({ ...form, question: e.target.value })}
                  placeholder="How long do I have to return an item?"
                />
              </div>
              <div className="field">
                <label htmlFor="doc-answer">Answer</label>
                <textarea
                  id="doc-answer"
                  required
                  rows={4}
                  value={form.answer}
                  onChange={(e) => setForm({ ...form, answer: e.target.value })}
                  placeholder="You can return any unused item within 60 days…"
                />
              </div>
            </>
          ) : (
            <div className="field">
              <label htmlFor="doc-content">Content</label>
              <textarea
                id="doc-content"
                required
                rows={7}
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
                placeholder="Paste your help article, policy, or product details here…"
              />
            </div>
          )}

          <div className="toolbar" style={{ marginBottom: 0 }}>
            <button className="btn btn-primary" type="submit" disabled={saving}>
              {saving
                ? "Saving…"
                : editingId
                  ? "Save changes"
                  : "Add document"}
            </button>
            {editingId && (
              <button
                className="btn btn-secondary"
                type="button"
                onClick={resetForm}
              >
                Cancel edit
              </button>
            )}
          </div>
        </form>
      </div>

      <div className="toolbar">
        <label className="btn btn-secondary" htmlFor="doc-upload">
          {uploading ? "Uploading…" : "Upload .txt / .md"}
        </label>
        <input
          id="doc-upload"
          ref={fileInputRef}
          type="file"
          accept=".txt,.md,text/plain,text/markdown"
          style={{ display: "none" }}
          onChange={(e) => void handleUpload(e.target.files?.[0])}
        />
        <span className="spacer" />
        <button
          className="btn btn-secondary"
          onClick={() => void handleReindex()}
          disabled={reindexing}
        >
          {reindexing ? "Reindexing…" : "Reindex all documents"}
        </button>
      </div>

      {loading ? (
        <p className="empty">Loading documents…</p>
      ) : documents.length === 0 ? (
        <p className="empty">
          No documents yet. Add your first one above and your agent can start
          answering questions.
        </p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Title</th>
                <th>Type</th>
                <th>Chunks</th>
                <th>Updated</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {documents.map((doc) => (
                <tr key={doc.id}>
                  <td>
                    <strong>{doc.title}</strong>
                  </td>
                  <td>{doc.type}</td>
                  <td>{doc.chunkCount}</td>
                  <td>{formatDate(doc.updatedAt)}</td>
                  <td>
                    <div className="row-actions">
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => {
                          setForm(formFromDocument(doc));
                          setEditingId(doc.id);
                          window.scrollTo(0, 0);
                        }}
                      >
                        Edit
                      </button>
                      <button
                        className="btn btn-danger btn-sm"
                        onClick={() => void handleDelete(doc)}
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
