using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportPilot.Api.Data;

namespace SupportPilot.Tests;

/// <summary>
/// Boots the real API against a fresh EF Core InMemory database per factory
/// instance (same pattern as the dotnet-saas-starter test factory), so tenant
/// isolation is exercised through the actual HTTP + EF filter pipeline.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "supportpilot-tests-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.IsGenericType && d.ServiceType.Name.Contains("IDbContextOptionsConfiguration"))).ToList();
            foreach (var d in toRemove) services.Remove(d);
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));
        });
    }
}
