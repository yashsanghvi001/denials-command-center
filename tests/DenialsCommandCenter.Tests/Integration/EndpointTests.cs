using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class EndpointTests(PostgresFixture pg) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Denials", pg.ConnectionString);
            builder.UseSetting("Ingestion:DataDirectory", TestData.Dir);
            builder.UseSetting("Ingestion:Today", "2026-09-30");
            builder.UseSetting("Auth:SeedPassword", TestAuth.Password);
        });
        _client = await TestAuth.LoginAsync(_factory, "manager");
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<JsonElement> Get(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Reconciliation_reports_zero_difference()
    {
        var reconciliation = await Get("/api/reconciliation");
        Assert.Equal(0m, reconciliation.GetProperty("difference").GetDecimal());
        Assert.Equal(118220.36m, reconciliation.GetProperty("payerFilesTotal").GetDecimal());
        Assert.Equal(4, reconciliation.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public async Task Exceptions_can_be_filtered_by_kind()
    {
        var unmatched = await Get("/api/exceptions?kind=UnmatchedRemittance");
        Assert.Equal(3, unmatched.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, unmatched.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Reference_describes_reason_codes_and_policy_sections()
    {
        var reference = await Get("/api/reference");

        Assert.Equal("The diagnosis is inconsistent with the procedure.", reference.GetProperty("reasonCodes").GetProperty("11").GetString());
        Assert.Equal("Missing patient medical record for this service.", reference.GetProperty("remarkCodes").GetProperty("M127").GetString());
        var section = reference.GetProperty("policySections").GetProperty("MPPO_DX-EXCL-03 §3");
        Assert.StartsWith("Meridian PPO", section.GetProperty("policyTitle").GetString());
        Assert.Contains("more specific code", section.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Exceptions_are_paged_searched_and_sorted()
    {
        var all = await Get("/api/exceptions");
        var firstPage = await Get("/api/exceptions?pageSize=2");
        var byAmount = await Get("/api/exceptions?sort=amount&direction=desc&pageSize=100");
        var searched = await Get("/api/exceptions?search=unmatched");

        var total = all.GetProperty("totalItems").GetInt32();
        Assert.Equal((2, total, (total + 1) / 2), (firstPage.GetProperty("items").GetArrayLength(), firstPage.GetProperty("totalItems").GetInt32(), firstPage.GetProperty("totalPages").GetInt32()));
        Assert.Contains("UnmatchedRemittance", all.GetProperty("kinds").EnumerateArray().Select(k => k.GetString()));
        Assert.Equal("Error", all.GetProperty("items")[0].GetProperty("severity").GetString());
        var amounts = byAmount.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("amount")).Where(a => a.ValueKind == JsonValueKind.Number).Select(a => a.GetDecimal()).ToList();
        Assert.Equal(amounts.OrderDescending(), amounts);
        Assert.True(searched.GetProperty("totalItems").GetInt32() >= 3);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/exceptions?sort=reason")).StatusCode);
    }

    [Fact]
    public async Task Claim_detail_shows_the_full_timeline_in_order()
    {
        var claim = await Get("/api/claims/GPP-2026-000230");
        var statuses = claim.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("statusCode").GetString()).ToArray();
        Assert.Equal(new[] { "1", "22", "4" }, statuses);
        Assert.Equal("Denied", claim.GetProperty("state").GetProperty("status").GetString());
        Assert.Equal("197", claim.GetProperty("denialLines")[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Unknown_claim_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/claims/GPP-2026-999999")).StatusCode);

    [Fact]
    public async Task Rerunning_ingestion_is_a_no_op()
    {
        var response = await _client.PostAsync("/api/ingestion/run", null);
        response.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("NoChange", body.GetProperty("outcome").GetString());
    }
}
