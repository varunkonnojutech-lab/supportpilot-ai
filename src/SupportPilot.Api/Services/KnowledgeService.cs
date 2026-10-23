using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Models;

namespace SupportPilot.Api.Services;

/// <summary>Keeps knowledge chunks in sync with documents.</summary>
public class KnowledgeService
{
    private readonly AppDbContext _db;
    private readonly ChunkingService _chunker;
    public KnowledgeService(AppDbContext db, ChunkingService chunker) { _db = db; _chunker = chunker; }

    public async Task RechunkDocumentAsync(KnowledgeDocument doc, CancellationToken ct = default)
    {
        var old = await _db.KnowledgeChunks.Where(c => c.DocumentId == doc.Id).ToListAsync(ct);
        _db.KnowledgeChunks.RemoveRange(old);
        var parts = _chunker.Chunk(doc.Content);
        for (var i = 0; i < parts.Count; i++)
            _db.KnowledgeChunks.Add(new KnowledgeChunk { DocumentId = doc.Id, Ordinal = i, Text = parts[i] });
    }

    public async Task<(int Documents, int Chunks)> ReindexTenantAsync(CancellationToken ct = default)
    {
        var docs = await _db.KnowledgeDocuments.ToListAsync(ct);
        var total = 0;
        foreach (var doc in docs)
        {
            await RechunkDocumentAsync(doc, ct);
            total += _chunker.Chunk(doc.Content).Count;
        }
        await _db.SaveChangesAsync(ct);
        return (docs.Count, total);
    }
}
