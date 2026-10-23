using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SupportPilot.Tests;

public static class TestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<(HttpClient Client, string Token)> RegisterAsync(ApiFactory factory, string business, string email)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new { businessName = business, email, password = "Pass123!" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = body.GetProperty("token").GetString()!;
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (authed, token);
    }

    public static async Task<JsonElement> PostJsonAsync(HttpClient client, string url, object payload)
    {
        var res = await client.PostAsJsonAsync(url, payload);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
    }
}
