using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using SupportPilot.Api.Data;
using SupportPilot.Api.Dtos;
using SupportPilot.Api.Models;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _config;

    public BillingController(AppDbContext db, ITenantContext tenantContext, IConfiguration config)
    { _db = db; _tenantContext = tenantContext; _config = config; }

    private bool Configured => !string.IsNullOrWhiteSpace(_config["Stripe:SecretKey"]);
    private const string NotConfiguredMessage = "Billing is not configured. Set the Stripe keys to enable upgrades.";

    private async Task<Models.Tenant?> CurrentTenantAsync() =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId);

    private async Task<string> EnsureCustomerAsync(Models.Tenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.StripeCustomerId)) return tenant.StripeCustomerId;
        var customer = await new CustomerService().CreateAsync(new CustomerCreateOptions
        {
            Name = tenant.Name,
            Metadata = new Dictionary<string, string> { ["tenantId"] = tenant.Id.ToString() }
        });
        tenant.StripeCustomerId = customer.Id;
        await _db.SaveChangesAsync();
        return customer.Id;
    }

    [Authorize]
    [HttpGet("status")]
    public async Task<ActionResult<BillingStatusResponse>> Status()
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Unauthorized(new ErrorResponse("Tenant not found."));
        return Ok(new BillingStatusResponse(Plans.ToApi(tenant.Plan), Configured));
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CheckoutRequest req)
    {
        if (!Configured) return BadRequest(new ErrorResponse(NotConfiguredMessage));
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Unauthorized(new ErrorResponse("Tenant not found."));
        var plan = Plans.Normalize(req.Plan);
        if (plan == Plans.Free) return BadRequest(new ErrorResponse("plan must be \"starter\" or \"pro\"."));
        var priceId = plan == Plans.Starter ? _config["Stripe:StarterPriceId"] : _config["Stripe:ProPriceId"];
        if (string.IsNullOrWhiteSpace(priceId)) return BadRequest(new ErrorResponse($"No Stripe price configured for the {Plans.ToApi(plan)} plan."));
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var customerId = await EnsureCustomerAsync(tenant);
        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            SuccessUrl = _config["Stripe:SuccessUrl"] ?? "http://localhost:5173/billing?status=success",
            CancelUrl = _config["Stripe:CancelUrl"] ?? "http://localhost:5173/billing?status=cancelled",
            LineItems = new List<SessionLineItemOptions> { new() { Price = priceId, Quantity = 1 } },
            Metadata = new Dictionary<string, string> { ["tenantId"] = tenant.Id.ToString(), ["plan"] = Plans.ToApi(plan) },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["tenantId"] = tenant.Id.ToString(), ["plan"] = Plans.ToApi(plan) }
            }
        });
        return Ok(new CheckoutResponse(session.Url));
    }

    [Authorize]
    [HttpPost("portal")]
    public async Task<ActionResult<CheckoutResponse>> Portal()
    {
        if (!Configured) return BadRequest(new ErrorResponse(NotConfiguredMessage));
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Unauthorized(new ErrorResponse("Tenant not found."));
        if (string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
            return BadRequest(new ErrorResponse("No billing account exists for this business yet. Choose a plan first."));
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = tenant.StripeCustomerId,
            ReturnUrl = _config["Stripe:PortalReturnUrl"] ?? "http://localhost:5173/billing"
        });
        return Ok(new CheckoutResponse(session.Url));
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var secret = _config["Stripe:WebhookSecret"];
        if (!Configured || string.IsNullOrWhiteSpace(secret))
            return BadRequest(new ErrorResponse(NotConfiguredMessage));
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], secret);
        }
        catch (StripeException)
        {
            return BadRequest(new ErrorResponse("Invalid webhook signature."));
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            {
                var session = (Session)stripeEvent.Data.Object;
                if (session.Metadata.TryGetValue("tenantId", out var tid) && Guid.TryParse(tid, out var tenantId) &&
                    session.Metadata.TryGetValue("plan", out var plan))
                {
                    var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                    if (tenant is not null)
                    {
                        tenant.Plan = Plans.Normalize(plan);
                        tenant.StripeCustomerId = session.CustomerId ?? tenant.StripeCustomerId;
                        tenant.StripeSubscriptionId = session.SubscriptionId ?? tenant.StripeSubscriptionId;
                        await _db.SaveChangesAsync();
                    }
                }
                break;
            }
            case "customer.subscription.updated":
            {
                var sub = (Subscription)stripeEvent.Data.Object;
                var tenant = await FindTenantForSubscriptionAsync(sub);
                if (tenant is not null && sub.Metadata.TryGetValue("plan", out var plan))
                {
                    tenant.Plan = Plans.Normalize(plan);
                    tenant.StripeSubscriptionId = sub.Id;
                    await _db.SaveChangesAsync();
                }
                break;
            }
            case "customer.subscription.deleted":
            {
                var sub = (Subscription)stripeEvent.Data.Object;
                var tenant = await FindTenantForSubscriptionAsync(sub);
                if (tenant is not null)
                {
                    tenant.Plan = Plans.Free;
                    tenant.StripeSubscriptionId = null;
                    await _db.SaveChangesAsync();
                }
                break;
            }
        }
        return Ok();
    }

    private async Task<Models.Tenant?> FindTenantForSubscriptionAsync(Subscription sub)
    {
        if (sub.Metadata.TryGetValue("tenantId", out var tid) && Guid.TryParse(tid, out var tenantId))
        {
            var byMeta = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (byMeta is not null) return byMeta;
        }
        return await _db.Tenants.FirstOrDefaultAsync(t => t.StripeCustomerId == sub.CustomerId);
    }
}
