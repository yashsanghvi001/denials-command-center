using System.Net;
using DenialsCommandCenter.Api.Ingestion;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class ClaimAccessTests(PostgresFixture pg) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    // Work items get their assignees only when users exist, and other test classes may have ingested before
    // the app seeded users, so rebuild the work items from a clean slate.
    public async Task InitializeAsync()
    {
        _factory = TestAuth.Factory(pg);
        await TestAuth.LoginAsync(_factory, "manager");
        await using var db = pg.NewDb();
        await db.AuditEntries.ExecuteDeleteAsync();
        await db.WorkNotes.ExecuteDeleteAsync();
        await db.WorkItems.ExecuteDeleteAsync();
        await new IngestionService(db, new IngestionOptions(TestData.Dir, new DateOnly(2026, 9, 30)), NullLogger<IngestionService>.Instance)
            .RunAsync(force: true);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<(string Own, string Other)> ClaimsFor(string username)
    {
        await using var db = pg.NewDb();
        var own = await db.WorkItems.Where(w => w.AssignedTo == username).Select(w => w.ClaimId).FirstAsync();
        var other = await db.WorkItems.Where(w => w.AssignedTo != username).Select(w => w.ClaimId).FirstAsync();
        return (own, other);
    }

    [Fact]
    public async Task Specialist_opens_own_claim_but_not_another()
    {
        var client = await TestAuth.LoginAsync(_factory, "priya");
        var (own, other) = await ClaimsFor("priya");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/claims/{own}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/denials/{own}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/claims/{other}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/denials/{other}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/denials/{other}/draft", null)).StatusCode);
    }

    [Fact]
    public async Task Specialist_cannot_open_a_paid_claim_that_has_no_work_item()
    {
        var client = await TestAuth.LoginAsync(_factory, "priya");
        await using var db = pg.NewDb();
        var paid = await db.ClaimStates.Where(s => s.Status == "Paid").Select(s => s.ClaimId).FirstAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/claims/{paid}")).StatusCode);
    }

    [Fact]
    public async Task Manager_opens_any_claim()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        var (_, other) = await ClaimsFor("priya");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/claims/{other}")).StatusCode);
    }
}
