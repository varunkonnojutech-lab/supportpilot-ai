namespace SupportPilot.Api.Services;

/// <summary>
/// Splits a document into ~800-character chunks with ~100 characters of
/// overlap, preferring to break on paragraph / sentence boundaries so a
/// chunk never cuts a thought in half when it can be avoided.
/// </summary>
public class ChunkingService
{
    public const int ChunkSize = 800;
    public const int Overlap = 100;

    public IReadOnlyList<string> Chunk(string? content)
    {
        var text = (content ?? string.Empty).Replace("\r\n", "\n").Trim();
        var chunks = new List<string>();
        if (text.Length == 0) return chunks;
        if (text.Length <= ChunkSize) { chunks.Add(text); return chunks; }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(text.Length, start + ChunkSize);
            if (end < text.Length)
            {
                // Backtrack to the last paragraph or sentence boundary in the back half.
                var windowStart = start + ChunkSize / 2;
                var para = text.LastIndexOf("\n\n", end, end - windowStart, StringComparison.Ordinal);
                var nl = text.LastIndexOf('\n', end, end - windowStart);
                var sent = text.LastIndexOf(". ", end, end - windowStart, StringComparison.Ordinal);
                var best = Math.Max(para >= 0 ? para + 2 : -1, Math.Max(nl >= 0 ? nl + 1 : -1, sent >= 0 ? sent + 2 : -1));
                if (best > windowStart) end = best;
            }
            var chunk = text[start..end].Trim();
            if (chunk.Length > 0) chunks.Add(chunk);
            if (end >= text.Length) break;
            var next = Math.Max(start + 1, end - Overlap);
            // Move to the next word boundary so overlap doesn't start mid-word.
            var space = text.IndexOf(' ', next);
            if (space > 0 && space < end) next = space + 1;
            if (next <= start) next = end;
            start = next;
        }
        return chunks;
    }
}
