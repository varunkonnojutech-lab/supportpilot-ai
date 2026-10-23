namespace SupportPilot.Api.Services;

public record Citation(string DocumentTitle, string Snippet);
public record RetrievedChunk(Guid ChunkId, Guid DocumentId, string DocumentTitle, string Text, double Score);
public record BrainAnswer(string Answer, IReadOnlyList<Citation> Citations);

/// <summary>What the brain needs to know about the business it speaks for.</summary>
public record TenantBrainContext(Guid TenantId, string BusinessName, IReadOnlyList<string> DocumentTitles);

public interface IChatBrain
{
    Task<BrainAnswer> AnswerAsync(TenantBrainContext tenant, string question, IReadOnlyList<RetrievedChunk> context, CancellationToken ct = default);
}

/// <summary>
/// Offline, zero-key brain. Retrieval is TF-IDF cosine over the tenant's own
/// chunks; the answer is extractive — the most relevant sentences from the top
/// chunks — so every claim it makes is traceable to a citation. Deterministic.
/// </summary>
public class OfflineBrainProvider : IChatBrain
{
    public const double MinScore = 0.03;

    public Task<BrainAnswer> AnswerAsync(TenantBrainContext tenant, string question, IReadOnlyList<RetrievedChunk> context, CancellationToken ct = default)
        => Task.FromResult(Compose(tenant, question, context));

    public static BrainAnswer Compose(TenantBrainContext tenant, string question, IReadOnlyList<RetrievedChunk> context)
    {
        var usable = context.Where(c => c.Score >= MinScore).Take(5).ToList();
        if (usable.Count == 0)
        {
            var topics = tenant.DocumentTitles.Count > 0
                ? string.Join(", ", tenant.DocumentTitles)
                : "our products and services";
            return new BrainAnswer(
                $"I'm sorry, I couldn't find information about that in {tenant.BusinessName}'s help content. " +
                $"I can help with topics like: {topics}. Could you rephrase your question, or ask about one of those topics?",
                Array.Empty<Citation>());
        }

        var questionTerms = TextUtils.Tokenize(question).Distinct().ToHashSet();
        var scored = new List<(double Rank, int Order, string Sentence, RetrievedChunk Chunk)>();
        var order = 0;
        foreach (var chunk in usable)
        {
            foreach (var sentence in TextUtils.SplitSentences(chunk.Text))
            {
                var tokens = TextUtils.Tokenize(sentence);
                if (tokens.Count == 0) continue;
                var hits = tokens.Count(t => questionTerms.Contains(t));
                // Rank primarily by which chunk matched, then by sentence relevance.
                var rank = chunk.Score * 100 + (questionTerms.Count == 0 ? 0 : (double)hits / Math.Max(1, questionTerms.Count) * 10) + (double)hits / tokens.Count;
                scored.Add((rank, order++, sentence, chunk));
            }
        }

        var picked = scored.OrderByDescending(s => s.Rank).Take(4)
            .OrderBy(s => usable.IndexOf(s.Chunk)).ThenBy(s => s.Order)
            .Select(s => s.Sentence).Distinct().ToList();
        if (picked.Count == 0) picked = usable.Select(c => TextUtils.Snippet(c.Text, 300)).ToList();

        var citations = usable.Take(3).Select(c => new Citation(c.DocumentTitle, TextUtils.Snippet(BestSentence(c, questionTerms) ?? c.Text))).ToList();
        return new BrainAnswer(string.Join(" ", picked), citations);
    }

    private static string? BestSentence(RetrievedChunk chunk, HashSet<string> questionTerms)
    {
        string? best = null; var bestHits = -1;
        foreach (var s in TextUtils.SplitSentences(chunk.Text))
        {
            var hits = TextUtils.Tokenize(s).Count(t => questionTerms.Contains(t));
            if (hits > bestHits) { bestHits = hits; best = s; }
        }
        return best;
    }
}

/// <summary>TF-IDF cosine retrieval over a tenant's stored chunks.</summary>
public class TfIdfRetrievalService
{
    public IReadOnlyList<RetrievedChunk> Retrieve(string question, IReadOnlyList<(Guid ChunkId, Guid DocumentId, string Title, string Text)> corpus, int top = 5)
    {
        if (corpus.Count == 0) return Array.Empty<RetrievedChunk>();
        var docs = corpus.Select(c => TextUtils.Tokenize(c.Text)).ToList();
        var df = new Dictionary<string, int>();
        foreach (var d in docs) foreach (var t in d.Distinct()) df[t] = df.GetValueOrDefault(t) + 1;
        double Idf(string t) => Math.Log((1.0 + corpus.Count) / (1.0 + df.GetValueOrDefault(t))) + 1.0;

        Dictionary<string, double> Vec(List<string> tokens)
        {
            var v = new Dictionary<string, double>();
            foreach (var t in tokens) v[t] = v.GetValueOrDefault(t) + 1.0;
            foreach (var k in v.Keys.ToList()) v[k] = (1 + Math.Log(v[k])) * Idf(k);
            return v;
        }
        double Cosine(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            double dot = 0, na = 0, nb = 0;
            foreach (var (k, val) in a) { na += val * val; if (b.TryGetValue(k, out var bv)) dot += val * bv; }
            foreach (var val in b.Values) nb += val * val;
            return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        var q = Vec(TextUtils.Tokenize(question));
        return corpus.Select((c, i) => new RetrievedChunk(c.ChunkId, c.DocumentId, c.Title, c.Text, Cosine(q, Vec(docs[i]))))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(top)
            .ToList();
    }
}
