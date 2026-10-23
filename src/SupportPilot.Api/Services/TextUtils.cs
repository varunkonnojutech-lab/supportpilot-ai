using System.Text.RegularExpressions;

namespace SupportPilot.Api.Services;

public static class TextUtils
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","is","are","was","were","be","been","being","do","does","did","can","could",
        "i","you","he","she","it","we","they","me","my","your","our","their","this","that","these",
        "those","of","to","in","on","for","with","and","or","but","if","then","so","as","at","by",
        "from","how","what","when","where","which","who","whom","will","would","should","may",
        "have","has","had","not","no","yes","please","there","here","am","about","into","than","too","very"
    };

    public static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
            .Select(m => m.Value)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToList();
    }

    public static List<string> SplitSentences(string text)
    {
        var parts = Regex.Split(text.Replace("\r", " ").Trim(), @"(?<=[.!?])\s+|\n+")
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        return parts;
    }

    public static string Snippet(string text, int max = 240)
    {
        var clean = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        return clean.Length <= max ? clean : clean[..max].TrimEnd() + "…";
    }
}
