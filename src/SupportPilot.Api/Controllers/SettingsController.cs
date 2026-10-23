using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Dtos;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings/widget")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public SettingsController(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    [HttpGet]
    public async Task<ActionResult<WidgetSettingsResponse>> Get()
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.TenantId);
        if (tenant is null) return NotFound(new ErrorResponse("Tenant not found."));
        return Ok(new WidgetSettingsResponse(tenant.WidgetKey, tenant.WidgetColor, tenant.WidgetGreeting, tenant.Name));
    }

    [HttpPut]
    public async Task<ActionResult<WidgetSettingsResponse>> Update(WidgetSettingsRequest req)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.TenantId);
        if (tenant is null) return NotFound(new ErrorResponse("Tenant not found."));
        if (!string.IsNullOrWhiteSpace(req.Color)) tenant.WidgetColor = req.Color.Trim();
        if (req.Greeting is not null) tenant.WidgetGreeting = req.Greeting.Trim();
        await _db.SaveChangesAsync();
        return Ok(new WidgetSettingsResponse(tenant.WidgetKey, tenant.WidgetColor, tenant.WidgetGreeting, tenant.Name));
    }

    [HttpPost("regenerate-key")]
    public async Task<ActionResult<RegenerateKeyResponse>> RegenerateKey()
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.TenantId);
        if (tenant is null) return NotFound(new ErrorResponse("Tenant not found."));
        tenant.WidgetKey = "sp_" + Guid.NewGuid().ToString("N")[..20];
        await _db.SaveChangesAsync();
        return Ok(new RegenerateKeyResponse(tenant.WidgetKey));
    }
}
