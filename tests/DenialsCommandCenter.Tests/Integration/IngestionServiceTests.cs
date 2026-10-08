using System.Text.Json;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class IngestionServiceTests(PostgresFixture pg)
{
    private static readonly IngestionOptions Options = new(TestData.Dir, new DateOnly(2026, 9, 30));

    private static IngestionService Service(DenialsDbContext db, IngestionOptions? options = null) =>
        new(db, options ?? Options, NullLogger<IngestionService>.Instance);

    private static async Task<string> Snapshot(DenialsDbContext db) => JsonSerializer.Serialize(new
    {
        claims = await db.Claims.OrderBy(c => c.ClaimId).ToListAsync(),
        events = await db.RemitEvents.OrderBy(e => e.EventKey).ToListAsync(),
        states = await db.ClaimStates.OrderBy(s => s.ClaimId).ToListAsync(),
        worklog = await db.WorklogEntries.OrderBy(w => w.RowNumber).ToListAsync(),
        issues = await db.IngestionIssues.OrderBy(i => i.Key).ToListAsync(),
        files = await db.SourceFiles.OrderBy(f => f.FileName).ToListAsync(),
    });

    [Fact]
    public async Task Rerun_on_same_files_changes_nothing_and_forced_rebuild_is_identical()
    {
        await using var db = pg.NewDb();
        var first = await Service(db).RunAsync(force: true);
        var before = await Snapshot(db);

        var second = await Service(db).RunAsync();
        var afterNoOp = await Snapshot(db);
        var third = await Service(db).RunAsync(force: true);
        var afterRebuild = await Snapshot(db);

        Assert.Equal("Applied", first.Outcome);
        Assert.Equal("NoChange", second.Outcome);
        Assert.Equal("Applied", third.Outcome);
        Assert.Equal(before, afterNoOp);
        Assert.Equal(before, afterRebuild);
    }

    [Fact]
    public async Task Changing_today_reapplies_even_when_files_are_unchanged()
    {
        await using var db = pg.NewDb();
        await Service(db).RunAsync(force: true);

        var nextDay = await Service(db, Options with { Today = Options.Today.AddDays(1) }).RunAsync();
        var backAgain = await Service(db).RunAsync();

        Assert.Equal("Applied", nextDay.Outcome);
        Assert.Equal("Applied", backAgain.Outcome);
    }

    [Fact]
    public async Task Persisted_numbers_match_the_golden_values()
    {
        await using var db = pg.NewDb();
        var summary = await Service(db).RunAsync(force: true);

        Assert.Equal(0m, summary.Difference);
        Assert.Equal(1197, await db.RemitEvents.CountAsync());
        Assert.Equal(117959.96m, await db.RemitEvents.Where(e => e.ClaimId != null).SumAsync(e => e.Paid));
        Assert.Equal(136, await db.ClaimStates.CountAsync(s => s.Status == "Denied"));
        Assert.Equal(31145.00m, await db.ClaimStates.SumAsync(s => s.DeniedAmount));
        Assert.Equal(3, await db.IngestionIssues.CountAsync(i => i.Kind == "UnmatchedRemittance"));
    }
}
