using Microsoft.AspNetCore.Identity;

namespace SupportPilot.Api.Models;

public interface ITenantOwned { Guid TenantId { get; set; } }

public static class Plans
{
    public const string Free = "Free";
    public const string Starter = "Starter";
    public const string Pro = "Pro";
    public static (int DocLimit, int ChatLimit) LimitsFor(string? plan) => plan switch
    {
        Starter => (50, 2000),
        Pro => (500, 20000),
        _ => (3, 100)
    };
    public static string Normalize(string? plan) => (plan ?? "").Trim().ToLowerInvariant() switch
    {
        "starter" => Starter,
        "pro" => Pro,
        _ => Free
    };
    public static string ToApi(string? plan) => Normalize(plan).ToLowerInvariant();
}

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Plan { get; set; } = Plans.Free;
    public string WidgetKey { get; set; } = string.Empty;
    public string WidgetColor { get; set; } = "#2563eb";
    public string WidgetGreeting { get; set; } = "Hi there! How can we help you today?";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class KnowledgeDocument : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>"text" or "qa" (lowercase, as sent by the API).</summary>
    public string Type { get; set; } = "text";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<KnowledgeChunk> Chunks { get; set; } = new();
}

public class KnowledgeChunk : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public KnowledgeDocument? Document { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class Conversation : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    /// <summary>"user" or "assistant" (lowercase).</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    /// <summary>null, "up" or "down".</summary>
    public string? Feedback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
