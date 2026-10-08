using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Ingestion;
using DenialsCommandCenter.Domain.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class AppealDraftServiceTests(PostgresFixture pg) : IAsyncLifetime
{
    private const string PayerErrorClaim = "GPP-2026-000230";
    private const string OtherRecoverableClaim = "GPP-2026-001893";

    private const string DailyQuotaBody = """
        {"error":{"code":429,"details":[{"@type":"type.googleapis.com/google.rpc.QuotaFailure","violations":[{"quotaId":"GenerateRequestsPerDayPerProjectPerModel-FreeTier"}]}]}}
        """;
    private static readonly IngestionOptions Options = new(TestData.Dir, new DateOnly(2026, 9, 30));
    private static readonly ReferenceData Reference = ReferenceDataLoader.Load(TestData.Dir);
    private static readonly GeminiOptions Configured = TestOptions.Gemini();
    private static readonly GeminiOptions NotConfigured = TestOptions.Gemini(apiKey: null);

    public async Task InitializeAsync()
    {
        await using var db = pg.NewDb();
        await new IngestionService(db, Options, NullLogger<IngestionService>.Instance).RunAsync();
        await db.AppealDrafts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string GeminiResponse(string rootCause, string citation, string letter) =>
        new JsonObject
        {
            ["candidates"] = new JsonArray(new JsonObject
            {
                ["content"] = new JsonObject
                {
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["text"] = new JsonObject
                        {
                            ["rootCauseCategory"] = rootCause, ["owningTeam"] = "Denials (appeal)", ["preventable"] = "No",
                            ["confidence"] = "High", ["letter"] = letter, ["citedSections"] = new JsonArray(citation),
                        }.ToJsonString(),
                    }),
                },
            }),
        }.ToJsonString();

    private static string GoodResponse() =>
        GeminiResponse("Payer error", "SMP_SNF-AUTH-2026 §3", "Re: {{CLAIM_ID}}. Per SMP_SNF-AUTH-2026 §3 no authorization was required.");

    private static (AppealDraftService Service, FakeGeminiHandler Handler) Service(
        DenialsDbContext db, GeminiOptions options, FakeGeminiHandler handler, AiCallGuard? guard = null) =>
        (new AppealDraftService(db, new GeminiClient(new HttpClient(handler), options), options, Reference,
            guard ?? new AiCallGuard(TimeProvider.System, options), TimeProvider.System, NullLogger<AppealDraftService>.Instance), handler);

    [Fact]
    public async Task Without_a_key_the_rule_template_is_used()
    {
        await using var db = pg.NewDb();
        var (service, handler) = Service(db, NotConfigured, new FakeGeminiHandler(GoodResponse()));

        var draft = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Template", "Template"), (draft!.Source, draft.Status));
        Assert.Contains("SMP_SNF-AUTH-2026 §3", draft.LetterTemplate);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Accepted_draft_is_cached()
    {
        await using var db = pg.NewDb();
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));

        var first = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);
        var second = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Gemini", "Accepted"), (first!.Source, first.Status));
        Assert.Equal(first.LetterTemplate, second!.LetterTemplate);
        Assert.Equal("Payer error", first.AiPrediction!.RootCause);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, await db.AppealDrafts.CountAsync());
        Assert.Equal(DraftStatuses.Accepted, (await service.FindCurrentLetterDraftAsync(PayerErrorClaim, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task Accepted_draft_survives_a_forced_ingestion_run()
    {
        await using var db = pg.NewDb();
        var (service, _) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));
        await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        await new IngestionService(db, Options, NullLogger<IngestionService>.Instance).RunAsync(force: true);

        Assert.Equal(1, await db.AppealDrafts.CountAsync());
    }

    [Fact]
    public async Task Request_to_gemini_carries_no_patient_identifiers()
    {
        await using var db = pg.NewDb();
        var claim = await db.Claims.AsNoTracking().SingleAsync(c => c.ClaimId == PayerErrorClaim);
        var state = await db.ClaimStates.AsNoTracking().SingleAsync(s => s.ClaimId == PayerErrorClaim);
        Assert.NotNull(state.DenialDate);
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));

        await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        var request = JsonNode.Parse(handler.LastRequestBody!)!;
        var sentText = string.Join('\n',
            request["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>(),
            request["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>());
        foreach (var identifier in new[] { "000230", claim.PatientFirst, claim.PatientLast, claim.MemberId, claim.RenderingNpi, claim.RenderingProvider,
                     claim.DateOfService.ToString("yyyy-MM-dd"), claim.PatientDob.ToString("yyyy-MM-dd"), state.DenialDate.Value.ToString("yyyy-MM-dd") })
            Assert.DoesNotContain(identifier, sentText);
        Assert.Equal("test-key", handler.LastApiKey);
    }

    [Fact]
    public async Task Each_claim_owns_its_own_cached_draft()
    {
        string[] payerErrorClaims = [PayerErrorClaim, "GPP-2026-000655", "GPP-2026-001665", "GPP-2026-001893"];
        await using var db = pg.NewDb();
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));

        foreach (var claimId in payerErrorClaims)
        {
            var draft = await service.DraftLetterAsync(claimId, CancellationToken.None);
            Assert.Equal(claimId, draft!.ClaimId);
            Assert.Equal("Accepted", draft.Status);
        }

        Assert.Equal(payerErrorClaims.Length, handler.Calls);
        Assert.Equal(payerErrorClaims.Length, await db.AppealDrafts.CountAsync());
    }

    [Fact]
    public async Task Concurrent_drafts_of_the_same_facts_share_one_model_call()
    {
        await using var firstDb = pg.NewDb();
        await using var secondDb = pg.NewDb();
        var release = new TaskCompletionSource();
        var handler = new FakeGeminiHandler(GoodResponse(), release.Task);
        var guard = new AiCallGuard(TimeProvider.System, Configured);
        var (first, _) = Service(firstDb, Configured, handler, guard);
        var (second, _) = Service(secondDb, Configured, handler, guard);

        var firstDraft = first.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);
        for (var waited = 0; handler.Calls == 0 && waited < 300; waited++) await Task.Delay(20);
        var secondDraft = second.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);
        await Task.Delay(1000);
        release.SetResult();
        var drafts = await Task.WhenAll(firstDraft, secondDraft);

        Assert.All(drafts, draft => Assert.Equal("Accepted", draft!.Status));
        Assert.Equal(1, handler.Calls);
        await using var db = pg.NewDb();
        Assert.Equal(1, await db.AppealDrafts.CountAsync());
    }

    [Fact]
    public async Task Quota_refusal_pauses_later_calls_instead_of_retrying()
    {
        await using var db = pg.NewDb();
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(HttpStatusCode.TooManyRequests, DailyQuotaBody));

        var refused = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);
        var paused = await service.DraftLetterAsync(OtherRecoverableClaim, CancellationToken.None);

        Assert.Equal(("Unavailable", "Unavailable"), (refused!.Status, paused!.Status));
        Assert.Contains("paused until", paused.StatusReason);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Daily_budget_stops_model_calls_but_cached_answers_are_still_served()
    {
        await using var db = pg.NewDb();
        var (service, handler) = Service(db, TestOptions.Gemini(dailyCallLimit: 1), new FakeGeminiHandler(GoodResponse()));

        var first = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);
        var overBudget = await service.DraftLetterAsync(OtherRecoverableClaim, CancellationToken.None);
        var cached = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Accepted", "Unavailable", "Accepted"), (first!.Status, overBudget!.Status, cached!.Status));
        Assert.Contains("budget", overBudget.StatusReason);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Answer_without_parts_falls_back_to_template()
    {
        await using var db = pg.NewDb();
        var (service, _) = Service(db, Configured, new FakeGeminiHandler("""{"candidates":[{"content":{"parts":[]}}]}"""));

        var draft = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Template", "Unavailable"), (draft!.Source, draft.Status));
        Assert.Equal(0, await db.AppealDrafts.CountAsync());
    }

    [Fact]
    public async Task Rejected_draft_falls_back_to_template()
    {
        await using var db = pg.NewDb();
        var (service, _) = Service(db, Configured, new FakeGeminiHandler(GeminiResponse("Payer error", "INVENTED-POLICY §1", "Re: {{CLAIM_ID}}.")));

        var draft = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Template", "Rejected"), (draft!.Source, draft.Status));
        Assert.Contains("INVENTED-POLICY §1", draft.StatusReason);
        Assert.Contains("SMP_SNF-AUTH-2026 §3", draft.LetterTemplate);
    }

    [Fact]
    public async Task Draft_whose_root_cause_disagrees_with_the_rules_is_rejected()
    {
        await using var db = pg.NewDb();
        var (service, _) = Service(db, Configured, new FakeGeminiHandler(GeminiResponse("Authorization", "SMP_SNF-AUTH-2026 §3", "Re: {{CLAIM_ID}}.")));

        var draft = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Template", "Rejected"), (draft!.Source, draft.Status));
        Assert.Equal("AI root cause 'Authorization' disagrees with the rules ('Payer error').", draft.StatusReason);
        Assert.Contains("SMP_SNF-AUTH-2026 §3", draft.LetterTemplate);
        Assert.Equal(DraftStatuses.Rejected, (await db.AppealDrafts.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("SMP_SNF-AUTH-2026 §4", "Rejected")]
    [InlineData("ALL_PAYERS_MOD25-2026 §1", "Accepted")]
    public async Task Draft_may_cite_only_the_rules_sections_or_all_payer_policy(string citation, string expectedStatus)
    {
        await using var db = pg.NewDb();
        var letter = $"Re: {{{{CLAIM_ID}}}}. Per {citation} the claim should be reprocessed.";
        var (service, _) = Service(db, Configured, new FakeGeminiHandler(GeminiResponse("Payer error", citation, letter)));

        var draft = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(expectedStatus, draft!.Status);
        if (expectedStatus == DraftStatuses.Rejected) Assert.Contains(citation, draft.StatusReason);
    }

    [Fact]
    public async Task Unavailable_service_falls_back_to_template()
    {
        await using var db = pg.NewDb();
        var (service, _) = Service(db, Configured, new FakeGeminiHandler(HttpStatusCode.TooManyRequests));

        var draft = await service.DraftLetterAsync(PayerErrorClaim, CancellationToken.None);

        Assert.Equal(("Template", "Unavailable"), (draft!.Source, draft.Status));
        Assert.NotNull(draft.LetterTemplate);
        Assert.Equal(0, await db.AppealDrafts.CountAsync());
    }

    [Fact]
    public async Task Write_off_claims_get_no_letter()
    {
        await using var db = pg.NewDb();
        var writeOff = await db.DenialAnalyses.AsNoTracking().FirstAsync(a => a.Action == "WriteOff");
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));

        var draft = await service.DraftLetterAsync(writeOff.ClaimId, CancellationToken.None);

        Assert.Equal(("None", "NotApplicable"), (draft!.Source, draft.Status));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Claims_outside_the_recoverable_bucket_get_no_letter()
    {
        await using var db = pg.NewDb();
        var expired = await db.DenialAnalyses.AsNoTracking()
            .FirstAsync(a => a.Bucket == "LostWindowExpired" && (a.Action == "Appeal" || a.Action == "CorrectedClaim"));
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));

        var draft = await service.DraftLetterAsync(expired.ClaimId, CancellationToken.None);

        Assert.Equal(("None", "NotApplicable", "No letter for LostWindowExpired claims."), (draft!.Source, draft.Status, draft.StatusReason));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Blind_classification_does_not_reveal_the_rule_result()
    {
        await using var db = pg.NewDb();
        var (service, handler) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));

        var draft = await service.ClassifyBlindAsync(PayerErrorClaim, CancellationToken.None);

        var userContent = JsonNode.Parse(handler.LastRequestBody!)!["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>();
        Assert.Null(JsonNode.Parse(userContent)!["ruleFinding"]);
        Assert.Equal("Payer error", draft!.AiPrediction!.RootCause);
    }

    [Fact]
    public async Task Unknown_claim_returns_null()
    {
        await using var db = pg.NewDb();
        var (service, _) = Service(db, Configured, new FakeGeminiHandler(GoodResponse()));
        Assert.Null(await service.DraftLetterAsync("GPP-2026-999999", CancellationToken.None));
    }

    private sealed class FakeGeminiHandler : HttpMessageHandler
    {
        private readonly string? _body;
        private readonly HttpStatusCode _status;
        private readonly int _callsToAwait;
        private readonly TaskCompletionSource _allCallsArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task? _release;
        private int _calls;

        public FakeGeminiHandler(string body, Task release) => (_body, _status, _callsToAwait, _release) = (body, HttpStatusCode.OK, 1, release);

        public FakeGeminiHandler(HttpStatusCode status, string body) => (_body, _status, _callsToAwait) = (body, status, 1);

        public FakeGeminiHandler(string body, int callsToAwait = 1) => (_body, _status, _callsToAwait) = (body, HttpStatusCode.OK, callsToAwait);

        public FakeGeminiHandler(HttpStatusCode status) => (_status, _callsToAwait) = (status, 1);

        public int Calls => _calls;
        public string? LastRequestBody { get; private set; }
        public string? LastApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) >= _callsToAwait) _allCallsArrived.TrySetResult();
            await _allCallsArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (_release is not null) await _release.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            LastRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastApiKey = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null;
            return new HttpResponseMessage(_status) { Content = new StringContent(_body ?? "{}", Encoding.UTF8, "application/json") };
        }
    }
}
