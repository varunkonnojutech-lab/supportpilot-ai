using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Models;

namespace SupportPilot.Api.Services;

public record ChatOutcome(string SessionId, Guid AssistantMessageId, string Answer, IReadOnlyList<Citation> Citations);

/// <summary>
/// Orchestrates one public widget chat turn for a tenant whose ambient
/// context has already been set: quota check -> retrieval -> brain -> persist.
/// </summary>
public class ChatService
{
    private readonly AppDbContext _db;
    private readonly TfIdfRetrievalService _retrieval;
    private readonly IChatBrain _brain;
    private readonly IConfiguration _config;

    public ChatService(AppDbContext db, TfIdfRetrievalService retrieval, IChatBrain brain, IConfiguration config)
    {
        _db = db; _retrieval = retrieval; _brain = brain; _config = config;
    }

    public async Task<int> ChatsThisMonthAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await _db.ChatMessages.CountAsync(m => m.Role == "user" && m.CreatedAt >= monthStart, ct);
    }

    public async Task<ChatOutcome> AskAsync(Tenant tenant, string? sessionId, string message, CancellationToken ct = default)
    {
        var sid = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId.Trim();
        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.SessionId == sid, ct);
        if (conversation is null)
        {
            conversation = new Conversation { SessionId = sid };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(ct);
        }

        var corpus = await _db.KnowledgeChunks
            .Select(c => new { c.Id, c.DocumentId, Title = c.Document != null ? c.Document.Title : "", c.Text })
            .ToListAsync(ct);
        var titles = await _db.KnowledgeDocuments.Select(d => d.Title).ToListAsync(ct);

        var provider = _config["Ai:Provider"] ?? "offline";
        IReadOnlyList<RetrievedChunk> context;
        if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            // Hand the embedding brain the full corpus (bounded) to rank itself.
            context = corpus.Take(500).Select(c => new RetrievedChunk(c.Id, c.DocumentId, c.Title, c.Text, 0)).ToList();
        }
        else
        {
            context = _retrieval.Retrieve(message, corpus.Select(c => (c.Id, c.DocumentId, c.Title, c.Text)).ToList());
        }

        var brainContext = new TenantBrainContext(tenant.Id, tenant.Name, titles);
        var result = await _brain.AnswerAsync(brainContext, message, context, ct);

        var now = DateTime.UtcNow;
        _db.ChatMessages.Add(new ChatMessage { ConversationId = conversation.Id, Role = "user", Content = message, CreatedAt = now });
        var assistant = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = result.Answer,
            CitationsJson = result.Citations.Count > 0 ? JsonSerializer.Serialize(result.Citations.Select(c => new Dtos.CitationDto(c.DocumentTitle, c.Snippet)).ToList(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null,
            CreatedAt = now.AddMilliseconds(1)
        };
        _db.ChatMessages.Add(assistant);
        conversation.LastMessageAt = now;
        await _db.SaveChangesAsync(ct);
        return new ChatOutcome(sid, assistant.Id, result.Answer, result.Citations);
    }
}
