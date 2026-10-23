using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Data;
using SupportPilot.Api.Dtos;
using SupportPilot.Api.Models;
using SupportPilot.Api.Services;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthController(UserManager<AppUser> users, AppDbContext db, JwtTokenService jwt)
    { _users = users; _db = db; _jwt = jwt; }

    public static readonly string[] Roles = { "Owner", "Admin", "Agent" };

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BusinessName) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new ErrorResponse("businessName, email and password are required."));
        if (await _users.FindByEmailAsync(req.Email) is not null)
            return BadRequest(new ErrorResponse("An account with that email already exists."));

        var tenant = new Tenant
        {
            Name = req.BusinessName.Trim(),
            Plan = Plans.Free,
            WidgetKey = "sp_" + Guid.NewGuid().ToString("N")[..20],
            WidgetColor = "#2563eb",
            WidgetGreeting = $"Hi there! Ask me anything about {req.BusinessName.Trim()}.",
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var user = new AppUser { UserName = req.Email, Email = req.Email, TenantId = tenant.Id, EmailConfirmed = true };
        var created = await _users.CreateAsync(user, req.Password);
        if (!created.Succeeded)
        {
            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync();
            return BadRequest(new ErrorResponse(string.Join(" ", created.Errors.Select(e => e.Description))));
        }
        await _users.AddToRoleAsync(user, "Owner");
        return Ok(new AuthResponse(_jwt.CreateToken(user, "Owner"), user.Email!, tenant.Name, "Owner", Plans.ToApi(tenant.Plan)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email ?? string.Empty);
        if (user is null || !await _users.CheckPasswordAsync(user, req.Password ?? string.Empty))
            return Unauthorized(new ErrorResponse("Invalid email or password."));
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Agent";
        return Ok(new AuthResponse(_jwt.CreateToken(user, role), user.Email!, tenant?.Name ?? "", role, Plans.ToApi(tenant?.Plan)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        var user = userId is not null && Guid.TryParse(userId, out var id) ? await _users.FindByIdAsync(id.ToString()) : null;
        if (user is null) return Unauthorized(new ErrorResponse("User not found."));
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
        var roles = await _users.GetRolesAsync(user);
        return Ok(new MeResponse(user.Email ?? "", tenant?.Name ?? "", roles.FirstOrDefault() ?? "Agent", Plans.ToApi(tenant?.Plan)));
    }
}
