using System.Globalization;
using System.Text.Json;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Appeals;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Reference;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DenialsCommandCenter.Api.Analysis;

public static class DraftPurposes
{
    public const string Letter = nameof(Letter);
    public const string Evaluation = nameof(Evaluation);
}

public static class DraftStatuses
{
    public const string Accepted = nameof(Accepted);
    public const string Rejected = nameof(Rejected);
    public const string Unavailable = nameof(Unavailable);
    public const string Template = nameof(Template);
    public const string NotApplicable = nameof(NotApplicable);
}

public sealed record AppealDraft(
    string ClaimId, string Source, string Status, string? StatusReason, string? LetterTemplate,
    IReadOnlyList<string> Citations, DenialPrediction? AiPrediction, string? AiConfidence);

public sealed class AppealDraftService(
    DenialsDbContext db, GeminiClient gemini, GeminiOptions geminiOptions, ReferenceData reference, AiCallGuard guard, TimeProvider clock, ILogger<AppealDraftService> log)
{
    // Keeps "§" readable to the model instead of a \u00A7 escape inside the facts.
    private static readonly JsonSerializerOptions FactsJsonOptions =
        new(JsonDefaults.Options) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly NextActionType[] ActionsWithLetters =
        [NextActionType.Appeal, NextActionType.CorrectedClaim, NextActionType.RequestRetroAuthorization];

    private sealed record DenialInputs(ClaimRecord Claim, ClaimState State, DenialClassification Finding, string Bucket);

    public async Task<AppealDraft?> DraftLetterAsync(string claimId, CancellationToken ct)
    {
        var inputs = await LoadAsync(claimId, ct);
        if (inputs is null) return null;
        var (_, _, finding, bucket) = inputs;
        if (!ActionsWithLetters.Contains(finding.Action))
            return NotApplicable(claimId, $"No letter is sent for action {finding.Action}.");
        if (bucket != nameof(RecoveryBucket.Recoverable))
            return NotApplicable(claimId, $"No letter for {bucket} claims.");

        return await GenerateAsync(claimId, LetterFacts(inputs), DraftPurposes.Letter, reference.Policies, TemplateFallback(inputs), ct);
    }

    // The letter the claim page shows without calling the AI again: the stored AI draft if it passed our checks,
    // otherwise the rule-based template. Null when no draft was ever requested for the current facts.
    public async Task<AppealDraft?> FindCurrentLetterAsync(string claimId, CancellationToken ct)
    {
        var inputs = await LoadAsync(claimId, ct);
        if (inputs is null) return null;
        var factsHash = LetterFactsHash(inputs);
        var row =await db.AppealDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.FactsHash == factsHash, ct);
        return row is null ? null : FromRow(row, claimId, TemplateFallback(inputs));
    }

    public async Task<AppealDraftRow?> FindCurrentLetterDraftAsync(string claimId, CancellationToken ct)
    {
        var factsHash = await CurrentLetterFactsHashAsync(claimId, ct);
        return factsHash is null ? null : await db.AppealDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.FactsHash == factsHash, ct);
    }

    public async Task<string?> CurrentLetterFactsHashAsync(string claimId, CancellationToken ct)
    {
        var inputs = await LoadAsync(claimId, ct);
        return inputs is null ? null : LetterFactsHash(inputs);
    }

    public async Task<AppealDraft?> ClassifyBlindAsync(string claimId, CancellationToken ct)
    {
        var inputs = await LoadAsync(claimId, ct);
        if (inputs is null) return null;
        var (claim, state, _, _) = inputs;
        var facts = AppealFactsBuilder.Build(claim, state, null, reference.PayerRules.GetValueOrDefault(claim.PayerId), reference.Policies, reference.ReasonCodes);
        return await GenerateAsync(claimId, facts, DraftPurposes.Evaluation, reference.Policies,
            (status, reason) => new AppealDraft(claimId, "None", status, reason, null, [], null, null), ct);
    }

    private Func<string, string?, AppealDraft> TemplateFallback(DenialInputs inputs)
    {
        var template = AppealTemplates.Letter(inputs.Finding, inputs.Claim.Payer, inputs.State.DeniedAmount, reference.Policies);
        return (status, reason) => new AppealDraft(inputs.Claim.ClaimId, "Template", status, reason, template, inputs.Finding.Citations, null, null);
    }

    private static AppealDraft NotApplicable(string claimId, string reason) =>
        new(claimId, "None", DraftStatuses.NotApplicable, reason, null, [], null, null);

    private AppealFacts LetterFacts(DenialInputs inputs) =>
        AppealFactsBuilder.Build(inputs.Claim, inputs.State, inputs.Finding, reference.PayerRules.GetValueOrDefault(inputs.Claim.PayerId),
            reference.Policies, reference.ReasonCodes);

    private string LetterFactsHash(DenialInputs inputs) => FactsHash(FactsJson(LetterFacts(inputs)), DraftPurposes.Letter, inputs.Claim.ClaimId);

    private static string FactsJson(AppealFacts facts) => JsonSerializer.Serialize(facts, FactsJsonOptions);

    private string FactsHash(string factsJson, string purpose, string claimId) => Hashing.Sha256Hex(
        $"{AppealPrompt.Version}|{AppealPrompt.SystemInstruction}|{AppealPrompt.ResponseSchema().ToJsonString()}|{geminiOptions.Model}|{purpose}|{claimId}|{factsJson}");

    private async Task<AppealDraft> GenerateAsync(
        string claimId, AppealFacts facts, string purpose, PolicyLibrary policies, Func<string, string?, AppealDraft> fallback, CancellationToken ct)
    {
        if (!geminiOptions.IsConfigured)
            return fallback(DraftStatuses.Template, "Gemini is not configured; the rule-based template is used.");

        var factsJson = FactsJson(facts);
        var factsHash = FactsHash(factsJson, purpose, claimId);
        var cached = await db.AppealDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.FactsHash == factsHash, ct);
        if (cached is not null) return FromRow(cached, claimId, fallback);

        if (guard.PausedUntil is { } pausedUntil)
            return fallback(DraftStatuses.Unavailable,
                $"AI drafting is paused until {pausedUntil.ToString("HH:mm", CultureInfo.InvariantCulture)} UTC because the AI service refused more requests; the rule-based template is used.");

        var startOfDay = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var dailyCallLimit = geminiOptions.DailyCallLimit!.Value; // [Required], so never null once the app has started
        if (await db.AppealDrafts.CountAsync(d => d.CreatedAt >= startOfDay, ct) >= dailyCallLimit)
            return fallback(DraftStatuses.Unavailable,
                $"Today's AI budget of {dailyCallLimit} calls is used up; the rule-based template is used.");

        string answer;
        try
        {
            // The shared call ignores the caller's cancellation so one user leaving does not fail everyone waiting on it.
            answer = await guard.CallOnceAsync(factsHash,
                    () => gemini.GenerateJsonAsync(AppealPrompt.SystemInstruction, factsJson, AppealPrompt.ResponseSchema(), CancellationToken.None))
                .WaitAsync(ct);
        }
        catch (GeminiUnavailableException ex) when (!ct.IsCancellationRequested)
        {
            guard.Pause(ex);
            log.LogWarning("Gemini refused the call for {ClaimId} ({StatusCode}); using the rule-based template.", claimId, (int?)ex.StatusCode);
            return fallback(DraftStatuses.Unavailable, "The AI service is unavailable; the rule-based template is used.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning("Gemini call failed for {ClaimId} ({ErrorType}); using the rule-based template.", claimId, ex.GetType().Name);
            return fallback(DraftStatuses.Unavailable, "The AI service is unavailable; the rule-based template is used.");
        }

        var row = new AppealDraftRow
        {
            FactsHash = factsHash, ClaimId = claimId, Purpose = purpose, Model = geminiOptions.Model,
            Status = DraftStatuses.Rejected, CitationsJson = "[]", CreatedAt = clock.GetUtcNow(),
        };
        var draft = TryParse(answer);
        if (draft is null)
        {
            row.StatusReason = "The AI answer did not match the expected format.";
        }
        else
        {
            var validation = DraftValidator.Validate(draft, policies);
            if (validation.Accepted && facts.RuleFinding is { } ruleFinding)
                validation = DraftValidator.CheckAgainstRules(draft, ruleFinding);
            row.Status = validation.Accepted ? DraftStatuses.Accepted : DraftStatuses.Rejected;
            row.StatusReason = validation.RejectionReason;
            row.LetterTemplate = validation.Accepted ? draft.Letter : null;
            row.CitationsJson = JsonSerializer.Serialize(draft.CitedSections, JsonDefaults.Options);
            row.AiRootCause = draft.RootCause;
            row.AiOwningTeam = draft.OwningTeam;
            row.AiPreventable = draft.Preventable;
            row.AiConfidence = draft.Confidence;
        }
        db.AppealDrafts.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(row).State = EntityState.Detached;
            var existing = await db.AppealDrafts.AsNoTracking().SingleAsync(d => d.FactsHash == factsHash, ct);
            return FromRow(existing, claimId, fallback);
        }
        return FromRow(row, claimId, fallback);
    }

    private static AppealDraft FromRow(AppealDraftRow row, string claimId, Func<string, string?, AppealDraft> fallback)
    {
        DenialPrediction? prediction = row.AiRootCause is null
            ? null
            : new DenialPrediction(row.AiRootCause, row.AiOwningTeam ?? "", row.AiPreventable ?? "");
        if (row.Status == DraftStatuses.Accepted)
            return new AppealDraft(claimId, "Gemini", row.Status, null, row.LetterTemplate,
                JsonSerializer.Deserialize<List<string>>(row.CitationsJson, JsonDefaults.Options) ?? [], prediction, row.AiConfidence);
        return fallback(row.Status, row.StatusReason) with { AiPrediction = prediction, AiConfidence = row.AiConfidence };
    }

    private static AiDraft? TryParse(string answer)
    {
        try
        {
            using var document = JsonDocument.Parse(answer);
            var root = document.RootElement;
            return new AiDraft(
                root.GetProperty("rootCauseCategory").GetString() ?? "",
                root.GetProperty("owningTeam").GetString() ?? "",
                root.GetProperty("preventable").GetString() ?? "",
                root.GetProperty("confidence").GetString() ?? "",
                root.GetProperty("letter").GetString() ?? "",
                root.GetProperty("citedSections").EnumerateArray().Select(e => e.GetString() ?? "").ToList());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<DenialInputs?> LoadAsync(string claimId, CancellationToken ct)
    {
        var analysis = await db.DenialAnalyses.AsNoTracking().SingleOrDefaultAsync(a => a.ClaimId == claimId, ct);
        if (analysis is null) return null;
        var claim = await db.Claims.AsNoTracking().SingleAsync(c => c.ClaimId == claimId, ct);
        var state = await db.ClaimStates.AsNoTracking().SingleAsync(s => s.ClaimId == claimId, ct);
        return new DenialInputs(RowMapping.ToClaimRecord(claim), RowMapping.ToClaimState(state),
            AnalysisPersistence.ToClassification(analysis), analysis.Bucket);
    }
}
