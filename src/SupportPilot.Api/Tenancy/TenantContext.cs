namespace SupportPilot.Api.Tenancy;

/// <summary>
/// Ambient tenant for the current request/scope. Authenticated requests get it
/// from the JWT "tenantId" claim; public widget requests set it explicitly
/// after resolving the tenant from the widget key. EF Core global query
/// filters read this, so tenant isolation is enforced by the data layer.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
}

public class TenantContext : ITenantContext
{
    public const string ClaimType = "tenantId";
    private readonly IHttpContextAccessor _accessor;
    private Guid? _override;

    public TenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid TenantId
    {
        get
        {
            if (_override is not null) return _override.Value;
            var claim = _accessor.HttpContext?.User.FindFirst(ClaimType)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public bool HasTenant => TenantId != Guid.Empty;
    public void SetTenant(Guid tenantId) => _override = tenantId;
}
