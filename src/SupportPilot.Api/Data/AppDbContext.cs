using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Models;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    private readonly ITenantContext _tenant;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options) => _tenant = tenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.WidgetKey).IsUnique();
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
        });
        builder.Entity<AppUser>(e =>
        {
            e.HasOne(u => u.Tenant).WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        // Global query filters: a tenant can only ever see its own rows.
        builder.Entity<KnowledgeDocument>(e =>
        {
            e.HasQueryFilter(d => d.TenantId == _tenant.TenantId);
            e.HasMany(d => d.Chunks).WithOne(c => c.Document).HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<KnowledgeChunk>(e => e.HasQueryFilter(c => c.TenantId == _tenant.TenantId));
        builder.Entity<Conversation>(e =>
        {
            e.HasQueryFilter(c => c.TenantId == _tenant.TenantId);
            e.HasMany(c => c.Messages).WithOne(m => m.Conversation).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ChatMessage>(e => e.HasQueryFilter(m => m.TenantId == _tenant.TenantId));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    private void Stamp()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty && _tenant.HasTenant)
                entry.Entity.TenantId = _tenant.TenantId;
        }
    }
}
