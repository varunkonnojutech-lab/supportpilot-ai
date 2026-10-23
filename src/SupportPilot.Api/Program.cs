using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportPilot.Api.Data;
using SupportPilot.Api.Models;
using SupportPilot.Api.Services;
using SupportPilot.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    // The chat widget is embedded on arbitrary customer sites, so the API
    // (and especially the public endpoints) must answer from any origin.
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=supportpilot.db"));

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SupportPilot",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SupportPilot",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ChunkingService>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<TfIdfRetrievalService>();
builder.Services.AddScoped<OfflineBrainProvider>();
builder.Services.AddScoped<OpenAiBrainProvider>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IChatBrain>(sp =>
{
    var provider = builder.Configuration["Ai:Provider"] ?? "offline";
    return provider.Equals("openai", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<OpenAiBrainProvider>()
        : sp.GetRequiredService<OfflineBrainProvider>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await SeedData.SeedAsync(app.Services);

app.Run();

public partial class Program { }
