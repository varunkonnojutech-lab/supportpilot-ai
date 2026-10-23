using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Dtos;
using SupportPilot.Api.Models;
using SupportPilot.Api.Services;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly KnowledgeService _knowledge;
    private readonly ITenantContext _tenantContext;
    private readonly ChatService _chat;

    public KnowledgeController(AppDbContext db, KnowledgeService knowledge, ITenantContext tenantContext, ChatService chat)
    { _db = db; _knowledge = knowledge; _tenantContext = tenantContext; _chat = chat; }

    private async Task<Tenant?> CurrentTenantAsync() =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId);

    private static DocumentResponse ToDto(KnowledgeDocument d, int chunkCount) =>
        new(d.Id, d.Title, d.Type, d.Content, chunkCount, d.CreatedAt, d.UpdatedAt);

    [HttpGet("documents")]
    public async Task<ActionResult<List<DocumentResponse>>> List()
    {
        var docs = await _db.KnowledgeDocuments.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        var counts = await _db.KnowledgeChunks.GroupBy(c => c.DocumentId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return Ok(docs.Select(d => ToDto(d, counts.GetValueOrDefault(d.Id))).ToList());
    }

    [HttpPost("documents")]
    public async Task<ActionResult<DocumentResponse>> Create(DocumentRequest req)
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Unauthorized(new ErrorResponse("Tenant not found."));
        var count = await _db.KnowledgeDocuments.CountAsync();
        var (docLimit, _) = Plans.LimitsFor(tenant.Plan);
        if (count >= docLimit)
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse($"Your {Plans.ToApi(tenant.Plan)} plan allows up to {docLimit} knowledge documents. Upgrade to add more."));
        var doc = new KnowledgeDocument
        {
            Title = (req.Title ?? string.Empty).Trim(),
            Type = (req.Type ?? "text").Trim().ToLowerInvariant() == "qa" ? "qa" : "text",
            Content = req.Content ?? string.Empty,
        };
        if (doc.Title.Length == 0) return BadRequest(new ErrorResponse("title is required."));
        _db.KnowledgeDocuments.Add(doc);
        await _db.SaveChangesAsync();
        await _knowledge.RechunkDocumentAsync(doc);
        await _db.SaveChangesAsync();
        var chunks = await _db.KnowledgeChunks.CountAsync(c => c.DocumentId == doc.Id);
        return Ok(ToDto(doc, chunks));
    }

    [HttpPut("documents/{id:guid}")]
    public async Task<ActionResult<DocumentResponse>> Update(Guid id, DocumentRequest req)
    {
        var doc = await _db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new ErrorResponse("Document not found."));
        doc.Title = (req.Title ?? doc.Title).Trim();
        doc.Type = (req.Type ?? doc.Type).Trim().ToLowerInvariant() == "qa" ? "qa" : "text";
        doc.Content = req.Content ?? string.Empty;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _knowledge.RechunkDocumentAsync(doc);
        await _db.SaveChangesAsync();
        var chunks = await _db.KnowledgeChunks.CountAsync(c => c.DocumentId == doc.Id);
        return Ok(ToDto(doc, chunks));
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await _db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new ErrorResponse("Document not found."));
        _db.KnowledgeDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("upload")]
    public async Task<ActionResult<DocumentResponse>> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new ErrorResponse("No file uploaded."));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".txt" or ".md")) return BadRequest(new ErrorResponse("Only .txt and .md files are supported."));
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Unauthorized(new ErrorResponse("Tenant not found."));
        var count = await _db.KnowledgeDocuments.CountAsync();
        var (docLimit, _) = Plans.LimitsFor(tenant.Plan);
        if (count >= docLimit)
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse($"Your {Plans.ToApi(tenant.Plan)} plan allows up to {docLimit} knowledge documents. Upgrade to add more."));
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        var doc = new KnowledgeDocument { Title = Path.GetFileName(file.FileName), Type = "text", Content = content };
        _db.KnowledgeDocuments.Add(doc);
        await _db.SaveChangesAsync();
        await _knowledge.RechunkDocumentAsync(doc);
        await _db.SaveChangesAsync();
        var chunks = await _db.KnowledgeChunks.CountAsync(c => c.DocumentId == doc.Id);
        return Ok(ToDto(doc, chunks));
    }

    [HttpPost("reindex")]
    public async Task<ActionResult<ReindexResponse>> Reindex()
    {
        var (documents, chunks) = await _knowledge.ReindexTenantAsync();
        return Ok(new ReindexResponse(documents, chunks));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<StatsResponse>> Stats()
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Unauthorized(new ErrorResponse("Tenant not found."));
        var (docLimit, chatLimit) = Plans.LimitsFor(tenant.Plan);
        var documents = await _db.KnowledgeDocuments.CountAsync();
        var chunks = await _db.KnowledgeChunks.CountAsync();
        var chats = await _chat.ChatsThisMonthAsync();
        return Ok(new StatsResponse(documents, chunks, docLimit, chats, chatLimit));
    }
}
