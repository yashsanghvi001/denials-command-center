using System.Text.Json;
using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Worklist;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Ingestion;

public static class IngestionOutcomes
{
    public const string Applied = nameof(Applied);
    public const string NoChange = nameof(NoChange);
}

public sealed record IngestionRunSummary(int RunId, string Outcome, string InputHash, int Events, int Issues, decimal Difference);

public sealed class IngestionService(DenialsDbContext db, IngestionOptions options, ILogger<IngestionService> log)
{
    // Arbitrary but fixed: every API instance must take the same Postgres advisory lock so runs never overlap.
    private const long IngestionLockKey = 8352026;

    public async Task<IngestionRunSummary> RunAsync(bool force = false, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({IngestionLockKey})", ct);

        var inputHash = DataPackLoader.InputHash(options.DataDir, options.Today);
        var last = await db.IngestionRuns
            .Where(r => r.Outcome == IngestionOutcomes.Applied)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);

        IngestionRun run;
        if (!force && last is not null && last.InputHash == inputHash && await db.WorkItems.AnyAsync(ct))
        {
            run = new IngestionRun
            {
                StartedAt = DateTimeOffset.UtcNow, InputHash = inputHash, Outcome = IngestionOutcomes.NoChange,
                Events = last.Events, Issues = last.Issues, Difference = last.Difference,
            };
            log.LogInformation("Ingestion skipped: inputs unchanged ({Hash})", inputHash[..12]);
        }
        else
        {
            var result = IngestionPipeline.Run(DataPackLoader.Load(options.DataDir, options.Today));
            var analyses = DenialAnalyzer.Analyze(result, ReferenceDataLoader.LoadPayerRules(options.DataDir), options.Today);
            await db.Database.ExecuteSqlRawAsync(
                """TRUNCATE "Claims", "RemitEvents", "ClaimStates", "WorklogEntries", "IngestionIssues", "SourceFiles", "DenialAnalyses" """, ct);
            db.ChangeTracker.Clear();
            PipelinePersistence.AddAll(db, result);
            AnalysisPersistence.AddAll(db, analyses);
            await WorkItemSync.CreateMissingAsync(db, analyses, result.Worklog, ct);
            run = new IngestionRun
            {
                StartedAt = DateTimeOffset.UtcNow, InputHash = inputHash, Outcome = IngestionOutcomes.Applied,
                Events = result.Events.Count, Issues = result.Issues.Count, Difference = result.Reconciliation.Difference,
                ReportJson = JsonSerializer.Serialize(result.Reconciliation, JsonDefaults.Options),
            };
            log.LogInformation("Ingestion applied: {Events} events, {Issues} issues, difference {Difference}",
                run.Events, run.Issues, run.Difference);
        }

        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return new IngestionRunSummary(run.Id, run.Outcome, run.InputHash, run.Events, run.Issues, run.Difference);
    }
}
