using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportPilot.Api.Models;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public string CreateToken(AppUser user, string role)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        if (key.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
        var issuer = _config["Jwt:Issuer"] ?? "SupportPilot";
        var audience = _config["Jwt:Audience"] ?? "SupportPilot";
        var minutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 1440;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TenantContext.ClaimType, user.TenantId.ToString()),
            new(ClaimTypes.Role, role),
            new("role", role),
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddMinutes(minutes), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
