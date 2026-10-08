using System.Text.Json;
using DenialsCommandCenter.Api.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class DenialAnalysisPersistenceTests(PostgresFixture pg)
{
    private static readonly IngestionOptions Options = new(TestData.Dir, new DateOnly(2026, 9, 30));

    [Fact]
    public async Task Ingestion_persists_one_analysis_per_open_denial_and_rebuilds_identically()
    {
        await using var db = pg.NewDb();
        await new IngestionService(db, Options, NullLogger<IngestionService>.Instance).RunAsync(force: true);
        var first = JsonSerializer.Serialize(await db.DenialAnalyses.AsNoTracking().OrderBy(a => a.ClaimId).ToListAsync());

        await new IngestionService(db, Options, NullLogger<IngestionService>.Instance).RunAsync(force: true);
        var second = JsonSerializer.Serialize(await db.DenialAnalyses.AsNoTracking().OrderBy(a => a.ClaimId).ToListAsync());

        Assert.Equal(first, second);
        Assert.Equal(155, await db.DenialAnalyses.CountAsync());
        Assert.Equal(11650.00m, await db.DenialAnalyses.Where(a => a.Bucket == "Recoverable").SumAsync(a => a.DeniedAmount));
        var payerError = await db.DenialAnalyses.AsNoTracking().SingleAsync(a => a.ClaimId == "GPP-2026-000230");
        Assert.Equal(("Payer error", "Appeal", "High"), (payerError.RootCause, payerError.Action, payerError.Confidence));
        Assert.Contains("SMP_SNF-AUTH-2026 §3", JsonSerializer.Deserialize<List<string>>(payerError.CitationsJson)!);
    }
}
