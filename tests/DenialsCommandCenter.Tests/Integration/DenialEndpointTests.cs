using System.Net;
using System.Text.Json;
using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class DenialEndpointTests(PostgresFixture pg) : IAsyncLifetime
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
            builder.UseSetting("Gemini:ApiKey", "");
            builder.UseSetting("Auth:SeedPassword", TestAuth.Password);
        });
        _client = await TestAuth.LoginAsync(_factory, "manager");
        await using var db = pg.NewDb();
        await db.AppealDrafts.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<JsonElement> Send(HttpMethod method, string url)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(method, url));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private const string PayerErrorClaim = "GPP-2026-000230";

    private async Task SeedDraft(AppealDraftRow draft)
    {
        await using var db = pg.NewDb();
        db.AppealDrafts.Add(draft);
        await db.SaveChangesAsync();
    }

    private static AppealDraftRow LetterDraft(string factsHash, string claimId, string status) => new()
    {
        FactsHash = factsHash, ClaimId = claimId, Purpose = DraftPurposes.Letter, Model = "gemini-test",
        Status = status, CitationsJson = "[]", CreatedAt = DateTimeOffset.UtcNow,
    };

    private async Task<AppealDraftRow> CurrentLetterDraft(string claimId, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var factsHash = await scope.ServiceProvider.GetRequiredService<AppealDraftService>().CurrentLetterFactsHashAsync(claimId, CancellationToken.None);
        return LetterDraft(factsHash!, claimId, status);
    }

    [Fact]
    public async Task Summary_answers_how_much_is_stuck_and_recoverable()
    {
        var summary = await Send(HttpMethod.Get, "/api/denials/summary");

        Assert.Equal(155, summary.GetProperty("openDenials").GetInt32());
        Assert.Equal(31145.00m, summary.GetProperty("deniedAmount").GetDecimal());
        var recoverable = summary.GetProperty("buckets")[0];
        Assert.Equal("Recoverable", recoverable.GetProperty("bucket").GetString());
        Assert.Equal((55, 11650.00m, 7090.50m),
            (recoverable.GetProperty("claims").GetInt32(), recoverable.GetProperty("deniedAmount").GetDecimal(), recoverable.GetProperty("expectedValue").GetDecimal()));
        Assert.Equal(5, summary.GetProperty("dueWithin14Days").GetProperty("claims").GetInt32());
        Assert.Equal(2890.00m, summary.GetProperty("dueWithin30Days").GetProperty("deniedAmount").GetDecimal());
    }

    [Fact]
    public async Task List_puts_the_highest_priority_recoverable_claim_first_and_filters()
    {
        var all = await Send(HttpMethod.Get, "/api/denials");
        Assert.Equal(155, all.GetArrayLength());
        Assert.Equal("GPP-2026-001828", all[0].GetProperty("claimId").GetString());

        var lost = await Send(HttpMethod.Get, "/api/denials?bucket=LostPolicy");
        Assert.Equal(48, lost.GetArrayLength());
    }

    [Fact]
    public async Task Detail_shows_root_cause_action_and_citations()
    {
        var detail = await Send(HttpMethod.Get, "/api/denials/GPP-2026-000230");

        var analysis = detail.GetProperty("analysis");
        Assert.Equal("Payer error", analysis.GetProperty("rootCause").GetString());
        Assert.Equal("Appeal", analysis.GetProperty("action").GetString());
        Assert.Contains("SMP_SNF-AUTH-2026 §3", analysis.GetProperty("citations").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("draft").ValueKind);
    }

    [Fact]
    public async Task Draft_without_gemini_returns_merged_template_letter()
    {
        var draft = await Send(HttpMethod.Post, "/api/denials/GPP-2026-000230/draft");

        Assert.Equal(("Template", "Template"), (draft.GetProperty("source").GetString(), draft.GetProperty("status").GetString()));
        var letter = draft.GetProperty("letter").GetString()!;
        Assert.Contains("GPP-2026-000230", letter);
        Assert.Contains("SMP_SNF-AUTH-2026 §3", letter);
        Assert.DoesNotContain("{{", letter);
    }

    [Fact]
    public async Task Drafting_a_letter_is_audited()
    {
        await Send(HttpMethod.Post, $"/api/denials/{PayerErrorClaim}/draft");

        await using var db = pg.NewDb();
        Assert.True(await db.AuditEntries.AnyAsync(a => a.EntityId == PayerErrorClaim && a.Action == "LetterDrafted" && a.Actor == "manager"));
    }

    [Theory]
    [InlineData("/api/denials?bucket=Lost")]
    [InlineData("/api/exceptions?severity=Fatal")]
    [InlineData("/api/exceptions?page=30000000&pageSize=100")]
    public async Task Unknown_filters_are_rejected_instead_of_returning_nothing(string url) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(url)).StatusCode);

    [Fact]
    public async Task Unknown_claim_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/denials/GPP-2026-999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/api/denials/GPP-2026-999999/draft", null)).StatusCode);
    }

    [Fact]
    public async Task Review_queue_is_empty_when_every_rule_is_confident_and_no_ai_disagrees() =>
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/review-queue")).GetArrayLength());

    [Fact]
    public async Task Rules_evaluation_matches_all_forty_labels()
    {
        var report = await Send(HttpMethod.Get, "/api/evaluation");
        Assert.Equal((40, 40), (report.GetProperty("labeled").GetInt32(), report.GetProperty("fullyCorrect").GetInt32()));
    }

    [Fact]
    public async Task Ai_evaluation_without_a_key_is_a_conflict() =>
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync("/api/evaluation/ai", null)).StatusCode);

    [Fact]
    public async Task Review_queue_lists_a_claim_whose_ai_draft_was_rejected()
    {
        var rejected = await CurrentLetterDraft(PayerErrorClaim, DraftStatuses.Rejected);
        rejected.StatusReason = "bad citation";
        await SeedDraft(rejected);

        var queue = await Send(HttpMethod.Get, "/api/review-queue");

        var item = Assert.Single(queue.EnumerateArray());
        Assert.Equal(PayerErrorClaim, item.GetProperty("analysis").GetProperty("claimId").GetString());
        Assert.Contains("rejected", item.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Review_queue_lists_a_claim_where_the_ai_disagrees_with_the_rules()
    {
        await using var db = pg.NewDb();
        var disputedClaim = await db.DenialAnalyses.AsNoTracking()
            .Where(a => a.Bucket == "Recoverable" && a.ClaimId != PayerErrorClaim)
            .OrderBy(a => a.ClaimId).FirstAsync();
        var accepted = await CurrentLetterDraft(disputedClaim.ClaimId, DraftStatuses.Accepted);
        accepted.AiRootCause = "Disputed by AI";
        await SeedDraft(accepted);

        var queue = await Send(HttpMethod.Get, "/api/review-queue");

        var item = Assert.Single(queue.EnumerateArray());
        Assert.Equal(disputedClaim.ClaimId, item.GetProperty("analysis").GetProperty("claimId").GetString());
        Assert.Equal($"The AI classified this as 'Disputed by AI' but the rules say '{disputedClaim.RootCause}'.", item.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Review_queue_lists_a_claim_whose_ai_draft_has_low_confidence()
    {
        var accepted = await CurrentLetterDraft(PayerErrorClaim, DraftStatuses.Accepted);
        accepted.AiConfidence = "Low";
        await SeedDraft(accepted);

        var queue = await Send(HttpMethod.Get, "/api/review-queue");

        var item = Assert.Single(queue.EnumerateArray());
        Assert.Equal(PayerErrorClaim, item.GetProperty("analysis").GetProperty("claimId").GetString());
        Assert.Equal("The AI is not confident in its draft.", item.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Review_queue_lists_a_claim_where_the_ai_picks_another_owning_team()
    {
        var accepted = await CurrentLetterDraft(PayerErrorClaim, DraftStatuses.Accepted);
        (accepted.AiRootCause, accepted.AiOwningTeam, accepted.AiPreventable, accepted.AiConfidence) = ("Payer error", "Billing", "No", "High");
        await SeedDraft(accepted);

        var item = Assert.Single((await Send(HttpMethod.Get, "/api/review-queue")).EnumerateArray());

        Assert.Equal("The AI assigned this to 'Billing' but the rules say 'Denials (appeal)'.", item.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Detail_merges_the_cached_letter_with_the_claims_own_details()
    {
        await using var db = pg.NewDb();
        var claim = await db.Claims.AsNoTracking().FirstAsync(c => c.ClaimId == PayerErrorClaim);
        var accepted = await CurrentLetterDraft(PayerErrorClaim, DraftStatuses.Accepted);
        accepted.LetterTemplate = "Re: {{PATIENT_NAME}}, claim {{CLAIM_ID}}.";
        accepted.CitationsJson = """["SMP_SNF-AUTH-2026 §3"]""";
        await SeedDraft(accepted);

        var draft = (await Send(HttpMethod.Get, $"/api/denials/{PayerErrorClaim}")).GetProperty("draft");

        Assert.Equal("Gemini", draft.GetProperty("source").GetString());
        var letter = draft.GetProperty("letter").GetString()!;
        Assert.Equal($"Re: {claim.PatientFirst} {claim.PatientLast}, claim {PayerErrorClaim}.", letter);
        Assert.DoesNotContain("{{", letter);
    }

    [Fact]
    public async Task Detail_of_a_rejected_draft_reports_the_template_source_and_rule_citations()
    {
        var rejected = await CurrentLetterDraft(PayerErrorClaim, DraftStatuses.Rejected);
        rejected.StatusReason = "bad citation";
        rejected.CitationsJson = """["Invented section"]""";
        await SeedDraft(rejected);

        var draft = (await Send(HttpMethod.Get, $"/api/denials/{PayerErrorClaim}")).GetProperty("draft");

        Assert.Equal("Template", draft.GetProperty("source").GetString());
        Assert.Contains("SMP_SNF-AUTH-2026 §3", draft.GetProperty("citations").EnumerateArray().Select(c => c.GetString()));
        Assert.DoesNotContain("Invented section", draft.GetProperty("citations").EnumerateArray().Select(c => c.GetString()));
        var letter = draft.GetProperty("letter").GetString()!;
        Assert.Contains(PayerErrorClaim, letter);
        Assert.Contains("SMP_SNF-AUTH-2026 §3", letter);
        Assert.DoesNotContain("{{", letter);
    }

    [Fact]
    public async Task List_filters_by_payer_and_root_cause()
    {
        var byPayer = await Send(HttpMethod.Get, "/api/denials?payerId=SMP12");
        Assert.NotEqual(0, byPayer.GetArrayLength());
        Assert.All(byPayer.EnumerateArray(), item => Assert.Equal("SMP12", item.GetProperty("payerId").GetString()));

        var byRootCause = await Send(HttpMethod.Get, "/api/denials?rootCause=Payer%20error");
        Assert.Equal(7, byRootCause.GetArrayLength());
    }

    [Fact]
    public async Task Draft_for_facts_that_no_longer_match_the_claim_is_ignored()
    {
        var stale = LetterDraft("stale-hash", PayerErrorClaim, DraftStatuses.Rejected);
        stale.StatusReason = "bad citation";
        await SeedDraft(stale);

        var detail = await Send(HttpMethod.Get, $"/api/denials/{PayerErrorClaim}");
        var queue = await Send(HttpMethod.Get, "/api/review-queue");

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("draft").ValueKind);
        Assert.DoesNotContain(queue.EnumerateArray(), item => item.GetProperty("analysis").GetProperty("claimId").GetString() == PayerErrorClaim);
    }
}
