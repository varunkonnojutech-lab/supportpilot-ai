using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Dtos;
using SupportPilot.Api.Models;
using SupportPilot.Api.Services;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Controllers;

/// <summary>
/// Public widget endpoints — no auth. The tenant is resolved from the widget
/// key and set as the ambient tenant for the rest of the request, so all
/// data access below is automatically scoped to that one business.
/// </summary>
[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ChatService _chat;

    public PublicController(AppDbContext db, ITenantContext tenantContext, ChatService chat)
    { _db = db; _tenantContext = tenantContext; _chat = chat; }

    private async Task<Models.Tenant?> ResolveTenantAsync(string widgetKey)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.WidgetKey == widgetKey);
        if (tenant is not null) _tenantContext.SetTenant(tenant.Id);
        return tenant;
    }

    [HttpGet("widget/{widgetKey}")]
    public async Task<ActionResult<PublicWidgetResponse>> Widget(string widgetKey)
    {
        var tenant = await ResolveTenantAsync(widgetKey);
        if (tenant is null) return NotFound(new ErrorResponse("Unknown widget key."));
        return Ok(new PublicWidgetResponse(tenant.Name, tenant.WidgetColor, tenant.WidgetGreeting));
    }

    [HttpPost("chat")]
    [EnableRateLimiting("public-chat")]
    public async Task<ActionResult<PublicChatResponse>> Chat(PublicChatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.WidgetKey)) return BadRequest(new ErrorResponse("widgetKey is required."));
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest(new ErrorResponse("message is required."));
        var tenant = await ResolveTenantAsync(req.WidgetKey);
        if (tenant is null) return NotFound(new ErrorResponse("Unknown widget key."));

        var (_, chatLimit) = Plans.LimitsFor(tenant.Plan);
        var used = await _chat.ChatsThisMonthAsync();
        if (used >= chatLimit)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ErrorResponse($"This business has reached its monthly chat limit ({chatLimit}) on the {Plans.ToApi(tenant.Plan)} plan. Please try again next month or contact the business directly."));

        var outcome = await _chat.AskAsync(tenant, req.SessionId, req.Message.Trim());
        return Ok(new PublicChatResponse(
            outcome.SessionId,
            outcome.AssistantMessageId,
            outcome.Answer,
            outcome.Citations.Select(c => new CitationDto(c.DocumentTitle, c.Snippet)).ToList()));
    }

    [HttpPost("chat/feedback")]
    [EnableRateLimiting("public-chat")]
    public async Task<IActionResult> Feedback(FeedbackRequest req)
    {
        var rating = (req.Rating ?? string.Empty).Trim().ToLowerInvariant();
        if (rating is not ("up" or "down")) return BadRequest(new ErrorResponse("rating must be \"up\" or \"down\"."));
        // Messages are tenant-filtered; look the conversation up without the
        // filter, then scope explicitly by its tenant before touching data.
        var conversation = await _db.Conversations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.SessionId == req.SessionId);
        if (conversation is null) return NotFound(new ErrorResponse("Session not found."));
        _tenantContext.SetTenant(conversation.TenantId);
        var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == req.MessageId && m.ConversationId == conversation.Id);
        if (message is null) return NotFound(new ErrorResponse("Message not found."));
        message.Feedback = rating;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
