using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Dtos;

namespace SupportPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ConversationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ConversationSummaryResponse>>> List()
    {
        var convos = await _db.Conversations.OrderByDescending(c => c.LastMessageAt)
            .Select(c => new ConversationSummaryResponse(c.Id, c.SessionId, c.StartedAt, c.LastMessageAt, c.Messages.Count))
            .ToListAsync();
        return Ok(convos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationDetailResponse>> Get(Guid id)
    {
        var convo = await _db.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id);
        if (convo is null) return NotFound(new ErrorResponse("Conversation not found."));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var messages = convo.Messages.OrderBy(m => m.CreatedAt).Select(m =>
        {
            var citations = new List<CitationDto>();
            if (!string.IsNullOrWhiteSpace(m.CitationsJson))
            {
                try { citations = JsonSerializer.Deserialize<List<CitationDto>>(m.CitationsJson, options) ?? new(); }
                catch { citations = new(); }
            }
            return new MessageResponse(m.Id, m.Role, m.Content, citations, m.Feedback, m.CreatedAt);
        }).ToList();
        return Ok(new ConversationDetailResponse(convo.Id, convo.SessionId, convo.StartedAt, messages));
    }
}
