using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportPilot.Api.Services;
using Xunit;

namespace SupportPilot.Tests;

public class SupportPilotTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SupportPilotTests(ApiFactory factory) => _factory = factory;

    private static string UniqueEmail(string tag) => $"{tag}-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_And_Login_Return_Tokens()
    {
        var email = UniqueEmail("flow");
        var (authed, _) = await TestHelpers.RegisterAsync(_factory, "Flow Bikes", email);

        var me = await authed.GetFromJsonAsync<JsonElement>("/api/auth/me", TestHelpers.Json);
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Flow Bikes", me.GetProperty("businessName").GetString());
        Assert.Equal("free", me.GetProperty("plan").GetString());

        var anon = _factory.CreateClient();
        var login = await TestHelpers.PostJsonAsync(anon, "/api/auth/login", new { email, password = "Pass123!" });
        Assert.False(string.IsNullOrWhiteSpace(login.GetProperty("token").GetString()));
        Assert.Equal("Owner", login.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Tenant_B_Cannot_See_Tenant_A_Documents_Or_Conversations()
    {
        var (a, _) = await TestHelpers.RegisterAsync(_factory, "Alpha Gear", UniqueEmail("alpha"));
        var (b, _) = await TestHelpers.RegisterAsync(_factory, "Beta Gear", UniqueEmail("beta"));

        var doc = await TestHelpers.PostJsonAsync(a, "/api/knowledge/documents",
            new { title = "Alpha Secret Policy", type = "text", content = "Alpha customers get lifetime free repairs on every product we sell, no questions asked." });
        var docId = doc.GetProperty("id").GetString();

        var bDocs = await b.GetFromJsonAsync<JsonElement>("/api/knowledge/documents", TestHelpers.Json);
        Assert.Equal(0, bDocs.GetArrayLength());

        // B asking the API to update A's document gets a 404, not the document.
        var put = await b.PutAsJsonAsync($"/api/knowledge/documents/{docId}",
            new { title = "Hijacked", type = "text", content = "nope" });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        // A's chat history must not leak to B either.
        var widget = await a.GetFromJsonAsync<JsonElement>("/api/settings/widget", TestHelpers.Json);
        var key = widget.GetProperty("widgetKey").GetString();
        var anon = _factory.CreateClient();
        var chat = await TestHelpers.PostJsonAsync(anon, "/api/public/chat",
            new { widgetKey = key, message = "Do you offer lifetime free repairs?" });
        Assert.False(string.IsNullOrWhiteSpace(chat.GetProperty("sessionId").GetString()));

        var bConvos = await b.GetFromJsonAsync<JsonElement>("/api/conversations", TestHelpers.Json);
        Assert.Equal(0, bConvos.GetArrayLength());
        var aConvos = await a.GetFromJsonAsync<JsonElement>("/api/conversations", TestHelpers.Json);
        Assert.True(aConvos.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Chunking_Long_Document_Produces_Multiple_Chunks()
    {
        var chunker = new ChunkingService();
        var paragraphs = Enumerable.Range(1, 8).Select(i =>
            $"Paragraph {i} explains our workshop process in detail. We inspect every item carefully, document the condition, photograph any damage, and share a written estimate with the customer before doing any paid work at all.");
        var content = string.Join("\n\n", paragraphs);
        var chunks = chunker.Chunk(content);
        Assert.True(chunks.Count > 1, $"Expected >1 chunks, got {chunks.Count}");
        Assert.All(chunks, c => Assert.True(c.Length <= ChunkingService.ChunkSize + 50));

        // And through the API: chunkCount in stats reflects the chunking.
        var (client, _) = await TestHelpers.RegisterAsync(_factory, "Chunky Co", UniqueEmail("chunky"));
        await TestHelpers.PostJsonAsync(client, "/api/knowledge/documents", new { title = "Long Doc", type = "text", content });
        var stats = await client.GetFromJsonAsync<JsonElement>("/api/knowledge/stats", TestHelpers.Json);
        Assert.True(stats.GetProperty("chunks").GetInt32() > 1);
    }

    [Fact]
    public async Task Retrieval_Returns_Cited_Answer_From_Returns_Doc()
    {
        var (client, _) = await TestHelpers.RegisterAsync(_factory, "Trail Shop", UniqueEmail("trail"));
        await TestHelpers.PostJsonAsync(client, "/api/knowledge/documents", new
        {
            title = "Returns & Exchanges",
            type = "text",
            content = "You can return any unused tent within 60 days of purchase for a full refund. To return a tent, print the prepaid return label from our website and drop the package at any carrier location. Refunds arrive within 7 to 10 business days."
        });
        await TestHelpers.PostJsonAsync(client, "/api/knowledge/documents", new
        {
            title = "Store Hours",
            type = "text",
            content = "Our store is open Monday to Friday from 9 AM to 6 PM and Saturday from 10 AM to 4 PM. We are closed on Sundays and public holidays."
        });

        var widget = await client.GetFromJsonAsync<JsonElement>("/api/settings/widget", TestHelpers.Json);
        var key = widget.GetProperty("widgetKey").GetString();
        var anon = _factory.CreateClient();
        var chat = await TestHelpers.PostJsonAsync(anon, "/api/public/chat",
            new { widgetKey = key, message = "How do I return a tent?" });

        var answer = chat.GetProperty("answer").GetString();
        Assert.Contains("return", answer!, StringComparison.OrdinalIgnoreCase);
        var citations = chat.GetProperty("citations");
        Assert.True(citations.GetArrayLength() >= 1, "Expected at least one citation");
        Assert.Contains(citations.EnumerateArray(), c => c.GetProperty("documentTitle").GetString() == "Returns & Exchanges");
    }

    [Fact]
    public async Task Free_Plan_Fourth_Document_Is_Rejected_With_403()
    {
        var (client, _) = await TestHelpers.RegisterAsync(_factory, "Tiny Biz", UniqueEmail("tiny"));
        for (var i = 1; i <= 3; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/knowledge/documents",
                new { title = $"Doc {i}", type = "text", content = $"Content number {i} for the tiny business knowledge base." });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        var fourth = await client.PostAsJsonAsync("/api/knowledge/documents",
            new { title = "Doc 4", type = "text", content = "One document too many." });
        Assert.Equal(HttpStatusCode.Forbidden, fourth.StatusCode);
        var body = await fourth.Content.ReadFromJsonAsync<JsonElement>(TestHelpers.Json);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Public_Chat_Works_With_Widget_Key_And_Unknown_Key_Is_404()
    {
        var (client, _) = await TestHelpers.RegisterAsync(_factory, "Widget Co", UniqueEmail("widget"));
        await TestHelpers.PostJsonAsync(client, "/api/knowledge/documents", new
        {
            title = "Delivery Info",
            type = "text",
            content = "Delivery takes 3 to 5 business days nationwide and is free for orders over fifty dollars."
        });
        var widget = await client.GetFromJsonAsync<JsonElement>("/api/settings/widget", TestHelpers.Json);
        var key = widget.GetProperty("widgetKey").GetString();

        var anon = _factory.CreateClient();
        var info = await anon.GetFromJsonAsync<JsonElement>($"/api/public/widget/{key}", TestHelpers.Json);
        Assert.Equal("Widget Co", info.GetProperty("businessName").GetString());

        var chat = await TestHelpers.PostJsonAsync(anon, "/api/public/chat",
            new { widgetKey = key, message = "How long does delivery take?" });
        Assert.Contains("business days", chat.GetProperty("answer").GetString());
        Assert.True(chat.GetProperty("citations").GetArrayLength() >= 1);

        var unknown = await anon.PostAsJsonAsync("/api/public/chat",
            new { widgetKey = "sp_does_not_exist", message = "hello" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var unknownWidget = await anon.GetAsync("/api/public/widget/sp_does_not_exist");
        Assert.Equal(HttpStatusCode.NotFound, unknownWidget.StatusCode);
    }

    [Fact]
    public async Task Seeded_Demo_Tenant_Answers_Return_Policy_Question()
    {
        var anon = _factory.CreateClient();
        var info = await anon.GetFromJsonAsync<JsonElement>("/api/public/widget/sp_demo_acme", TestHelpers.Json);
        Assert.Equal("Acme Outdoors", info.GetProperty("businessName").GetString());
        Assert.Equal("#15803d", info.GetProperty("color").GetString());

        var chat = await TestHelpers.PostJsonAsync(anon, "/api/public/chat",
            new { widgetKey = "sp_demo_acme", message = "What is your return policy?" });
        Assert.Contains("60 days", chat.GetProperty("answer").GetString());
        Assert.True(chat.GetProperty("citations").GetArrayLength() >= 1);
    }
}
