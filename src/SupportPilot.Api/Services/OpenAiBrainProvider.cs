using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SupportPilot.Api.Services;

/// <summary>
/// OpenAI-compatible brain: embeds the question and the tenant's chunks,
/// cosine-ranks them, then asks the chat model to answer ONLY from the
/// retrieved context with citations. Any API failure falls back to the
/// offline extractive brain, so the widget never hard-fails on a key/quota
/// problem. Uses raw HttpClient — no vendor SDK.
/// </summary>
public class OpenAiBrainProvider : IChatBrain
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly OfflineBrainProvider _fallback;
    private readonly TfIdfRetrievalService _tfidf;
    private readonly ILogger<OpenAiBrainProvider> _logger;

    public OpenAiBrainProvider(IHttpClientFactory httpFactory, IConfiguration config, OfflineBrainProvider fallback, TfIdfRetrievalService tfidf, ILogger<OpenAiBrainProvider> logger)
    {
        _httpFactory = httpFactory; _config = config; _fallback = fallback; _tfidf = tfidf; _logger = logger;
    }

    private Task<BrainAnswer> FallbackAsync(TenantBrainContext tenant, string question, IReadOnlyList<RetrievedChunk> context, CancellationToken ct)
    {
        // If the caller handed us the unranked corpus (all scores 0), rank it
        // with TF-IDF first so the offline brain sees real relevance scores.
        var ranked = context.All(c => c.Score == 0) && context.Count > 0
            ? _tfidf.Retrieve(question, context.Select(c => (c.ChunkId, c.DocumentId, c.DocumentTitle, c.Text)).ToList())
            : context;
        return _fallback.AnswerAsync(tenant, question, ranked, ct);
    }

    public async Task<BrainAnswer> AnswerAsync(TenantBrainContext tenant, string question, IReadOnlyList<RetrievedChunk> context, CancellationToken ct = default)
    {
        // `context` here is the TF-IDF candidate set; we re-rank it with embeddings
        // when the caller gave us the full corpus via the chunks' text. To keep the
        // IChatBrain contract simple the chat service passes the full corpus as
        // context for this provider (score 0 = unranked).
        try
        {
            var apiKey = _config["OpenAi:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return await FallbackAsync(tenant, question, context, ct);
            var baseUrl = (_config["OpenAi:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');
            var chatModel = _config["OpenAi:ChatModel"] ?? "gpt-4o-mini";
            var embedModel = _config["OpenAi:EmbeddingModel"] ?? "text-embedding-3-small";
            if (context.Count == 0) return await FallbackAsync(tenant, question, context, ct);

            var http = _httpFactory.CreateClient("openai");
            http.Timeout = TimeSpan.FromSeconds(30);

            var inputs = new List<string> { question };
            inputs.AddRange(context.Select(c => c.Text));
            var embeddings = await EmbedAsync(http, baseUrl, apiKey, embedModel, inputs, ct);
            if (embeddings is null || embeddings.Count != inputs.Count)
                return await FallbackAsync(tenant, question, context, ct);

            var q = embeddings[0];
            var ranked = context.Select((c, i) => c with { Score = Cosine(q, embeddings[i + 1]) })
                .OrderByDescending(c => c.Score).Take(5).Where(c => c.Score > 0.15).ToList();
            if (ranked.Count == 0) return await FallbackAsync(tenant, question, context, ct);

            var contextText = string.Join("\n\n", ranked.Select((c, i) => $"[{i + 1}] From \"{c.DocumentTitle}\":\n{c.Text}"));
            var system = "You are a customer support assistant. Answer ONLY from the provided context. " +
                         "Be concise and helpful. If the context does not contain the answer, say you don't have that information. " +
                         "Mention the source document titles you used.";
            var user = $"Business: {tenant.BusinessName}\n\nContext:\n{contextText}\n\nCustomer question: {question}";
            var answer = await ChatAsync(http, baseUrl, apiKey, chatModel, system, user, ct);
            if (string.IsNullOrWhiteSpace(answer)) return await _fallback.AnswerAsync(tenant, question, ranked, ct);

            var citations = ranked.Take(3).Select(c => new Citation(c.DocumentTitle, TextUtils.Snippet(c.Text))).ToList();
            return new BrainAnswer(answer.Trim(), citations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI brain failed; falling back to offline brain.");
            return await FallbackAsync(tenant, question, context, ct);
        }
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++) { dot += a[i] * b[i]; na += (double)a[i] * a[i]; nb += (double)b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private async Task<List<float[]>?> EmbedAsync(HttpClient http, string baseUrl, string key, string model, List<string> inputs, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { model, input = inputs });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/embeddings") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var list = new List<float[]>();
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray().OrderBy(d => d.GetProperty("index").GetInt32()))
            list.Add(item.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray());
        return list;
    }

    private async Task<string?> ChatAsync(HttpClient http, string baseUrl, string key, string model, string system, string user, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model,
            messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            temperature = 0.2
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}
