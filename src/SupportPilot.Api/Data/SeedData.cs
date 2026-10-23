using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Api.Models;
using SupportPilot.Api.Services;
using SupportPilot.Api.Tenancy;

namespace SupportPilot.Api.Data;

/// <summary>
/// Idempotent startup seed: the "Acme Outdoors" demo tenant on the Pro plan
/// with a fixed widget key and five realistic knowledge documents, chunked
/// through the exact same pipeline as user-created documents.
/// </summary>
public static class SeedData
{
    public const string DemoEmail = "demo@acmeoutdoors.com";
    public const string DemoPassword = "Demo123!";
    public const string DemoWidgetKey = "sp_demo_acme";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Owner", "Admin", "Agent" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.WidgetKey == DemoWidgetKey);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Acme Outdoors",
                Plan = Plans.Pro,
                WidgetKey = DemoWidgetKey,
                WidgetColor = "#15803d",
                WidgetGreeting = "Hi there! Ask me anything about Acme Outdoors — shipping, returns, rentals and more.",
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        if (await userManager.FindByEmailAsync(DemoEmail) is null)
        {
            var user = new AppUser { UserName = DemoEmail, Email = DemoEmail, TenantId = tenant.Id, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, DemoPassword);
            if (result.Succeeded) await userManager.AddToRoleAsync(user, "Owner");
        }

        var tenantContext = sp.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(tenant.Id);
        if (await db.KnowledgeDocuments.AnyAsync()) return;

        var knowledge = sp.GetRequiredService<KnowledgeService>();
        foreach (var (title, content) in Documents())
        {
            var doc = new KnowledgeDocument { Title = title, Type = "text", Content = content };
            db.KnowledgeDocuments.Add(doc);
            await db.SaveChangesAsync();
            await knowledge.RechunkDocumentAsync(doc);
            await db.SaveChangesAsync();
        }
    }

    public static IReadOnlyList<(string Title, string Content)> Documents() => new List<(string, string)>
    {
        ("Shipping & Delivery", """
        Acme Outdoors offers several shipping options for orders placed within the United States. Standard shipping takes 3 to 5 business days and costs a flat $6.95. Standard shipping is free on all orders over $75. Express shipping takes 1 to 2 business days and costs $19.95. Overnight shipping is available for $34.95 on orders placed before 2 PM Mountain Time.

        Orders are processed within one business day from our Denver, Colorado warehouse. You will receive a tracking number by email as soon as your order ships. We ship to all 50 states, including Alaska and Hawaii, although delivery to Alaska and Hawaii can take an additional 2 to 3 business days. We do not currently ship internationally, and we cannot deliver to P.O. boxes for express or overnight orders.

        Oversized items such as tents, kayaks, and roof racks may ship in separate boxes and can take an extra business day to leave the warehouse. If an item is on backorder, we will email you with an updated delivery estimate and you may cancel the order at any time before it ships for a full refund.
        """),
        ("Returns & Exchanges", """
        We want you to love your gear. If you are not completely satisfied, you can return any unused item within 60 days of purchase for a full refund to your original payment method. Items must be in their original condition with tags attached. To start a return, visit the returns page on our website, enter your order number and email address, and print the prepaid return shipping label we email you.

        Return shipping is free for all returns within the contiguous United States. Refunds are processed within 5 business days of the returned item arriving at our warehouse, and most customers see the money back on their card within 7 to 10 business days of approval. Final sale and clearance items marked as final sale cannot be returned or exchanged.

        Exchanges are easy and free. If you need a different size or color of the same item, choose the exchange option during the return process and we will ship the replacement as soon as your return label is scanned, so you do not have to wait for the original item to arrive back. Used gear can be traded in through our ReGear program for store credit, but used items cannot be returned for a cash refund.
        """),
        ("Tent Rentals", """
        Acme Outdoors rents high-quality tents and camping gear from our Denver and Boulder store locations. Rental tents include 2-person, 4-person, and 6-person models from trusted brands, starting at $25 per day for a 2-person tent, $35 per day for a 4-person tent, and $45 per day for a 6-person tent. A refundable deposit of $100 is required for every tent rental, and the deposit is returned when the tent comes back clean and undamaged.

        Reservations can be made online or in store up to 60 days in advance, and we recommend booking early for holiday weekends. The minimum rental period is one day and the maximum is 14 days. Every rental includes the tent, rainfly, stakes, a footprint groundsheet, and a repair kit at no extra charge. Sleeping bags, sleeping pads, backpacks, and camp stoves can be added to any rental for $8 per item per day.

        Rentals must be picked up and returned at the same store during store hours. Please return tents dry and clean: a $25 cleaning fee applies to tents returned wet or muddy. Late returns are charged at the daily rate plus a $15 late fee per day. Cancellations made at least 48 hours before pickup receive a full refund of rental fees.
        """),
        ("Repairs & Warranty", """
        Every product we sell is covered by a minimum one-year warranty against defects in materials and workmanship. Many of our partner brands offer longer coverage, and Acme Outdoors branded gear carries a lifetime warranty for the original owner. If a product fails because of a manufacturing defect, we will repair it, replace it, or refund it, at our discretion.

        Our in-house repair shop handles zipper repairs, seam sealing, patch sewing, tent pole replacement, and backpack buckle repairs. Simple repairs such as zipper sliders and buckle swaps are usually finished within 3 to 5 business days and cost between $15 and $40. More complex repairs are quoted individually after inspection, and inspection is always free. You can drop gear off at any store or mail it to our Denver repair center with a note describing the problem.

        The warranty does not cover damage from accidents, misuse, normal wear and tear, or unauthorized alterations. Cosmetic damage that does not affect function, such as scratches or faded fabric, is not covered. To file a warranty claim, bring the item and your proof of purchase to any store, or start a claim on our website and we will email you a prepaid mailing label for warranty evaluations.
        """),
        ("Store Hours & Contact", """
        Our flagship Denver store is open Monday through Saturday from 9 AM to 9 PM and on Sunday from 10 AM to 6 PM. The Boulder store is open Monday through Saturday from 9 AM to 8 PM and on Sunday from 10 AM to 6 PM. Both stores are closed on Thanksgiving Day, Christmas Day, and New Year's Day, and close early at 4 PM on Christmas Eve.

        You can reach our customer support team by phone at (303) 555-0142, Monday through Friday from 8 AM to 6 PM Mountain Time. Email us any time at support@acmeoutdoors.example and we will reply within one business day. Live chat on our website is staffed during store hours, and this assistant can answer common questions around the clock.

        The Denver store is located at 1420 Platte Street, Denver, Colorado, and free parking is available behind the building. The Boulder store is located at 88 Pearl Street, Boulder, Colorado. Both locations offer free in-store pickup for online orders, usually ready within 2 hours during store hours.
        """),
    };
}
