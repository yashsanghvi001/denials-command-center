using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DenialsCommandCenter.Api.Ingestion;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class WorklistTests(PostgresFixture pg) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = TestAuth.Factory(pg);
        await TestAuth.LoginAsync(_factory, "manager");
        await using var db = pg.NewDb();
        await db.AuditEntries.ExecuteDeleteAsync();
        await db.WorkNotes.ExecuteDeleteAsync();
        await db.WorkItems.ExecuteDeleteAsync();
        await ReingestAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task ReingestAsync()
    {
        await using var db = pg.NewDb();
        await new IngestionService(db, new IngestionOptions(TestData.Dir, new DateOnly(2026, 9, 30)), NullLogger<IngestionService>.Instance)
            .RunAsync(force: true);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private async Task<string> ClaimAssignedTo(string username, string excludingStatus = "")
    {
        await using var db = pg.NewDb();
        return (await db.WorkItems.OrderBy(w => w.ClaimId).FirstAsync(w => w.AssignedTo == username && w.Status != excludingStatus)).ClaimId;
    }

    [Fact]
    public async Task Manager_sees_every_open_denial_with_the_highest_priority_first()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");

        var page = await Json(await client.GetAsync("/api/worklist"));
        var rows = page.GetProperty("items");

        Assert.Equal((155, 25, 7), (page.GetProperty("totalItems").GetInt32(), rows.GetArrayLength(), page.GetProperty("totalPages").GetInt32()));
        Assert.Equal("GPP-2026-001828", rows[0].GetProperty("claimId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(rows[0].GetProperty("patientName").GetString()));
        Assert.True((await Json(await client.GetAsync("/api/worklist?assignee=unassigned"))).GetProperty("totalItems").GetInt32() > 0);
    }

    [Fact]
    public async Task Worklog_owners_and_progress_seed_the_work_items()
    {
        await using var db = pg.NewDb();
        Assert.True(await db.WorkItems.AnyAsync(w => w.AssignedTo == "priya"));
        Assert.True(await db.WorkItems.AnyAsync(w => w.Status == "InProgress"));
        Assert.Equal(155, await db.AuditEntries.CountAsync(a => a.Action == "Created" && a.Actor == "system"));
    }

    [Fact]
    public async Task Specialist_sees_only_their_own_claims()
    {
        var client = await TestAuth.LoginAsync(_factory, "priya");
        await using var db = pg.NewDb();

        var page = await Json(await client.GetAsync("/api/worklist?assignee=karan&status=all&pageSize=100"));

        Assert.Equal(await db.WorkItems.CountAsync(w => w.AssignedTo == "priya"), page.GetProperty("totalItems").GetInt32());
        Assert.All(page.GetProperty("items").EnumerateArray(), row => Assert.Equal("priya", row.GetProperty("assignedTo").GetString()));
    }

    [Fact]
    public async Task Status_change_is_audited_with_before_and_after()
    {
        var claimId = await ClaimAssignedTo("priya", excludingStatus: "PendingPayer");
        var client = await TestAuth.LoginAsync(_factory, "priya");

        var item = await Json(await client.PatchAsJsonAsync($"/api/worklist/{claimId}/status", new { status = "PendingPayer" }));
        var detail = await Json(await client.GetAsync($"/api/worklist/{claimId}"));

        Assert.Equal("PendingPayer", item.GetProperty("status").GetString());
        var entry = detail.GetProperty("audit").EnumerateArray().Last();
        Assert.Equal(("priya", "StatusChanged"), (entry.GetProperty("actor").GetString(), entry.GetProperty("action").GetString()));
        Assert.Contains("PendingPayer", entry.GetProperty("afterJson").GetString());
        Assert.DoesNotContain("PendingPayer", entry.GetProperty("beforeJson").GetString());
    }

    [Fact]
    public async Task Invalid_status_is_rejected()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        var response = await client.PatchAsJsonAsync("/api/worklist/GPP-2026-000230/status", new { status = "Done" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Notes_are_saved_audited_and_validated()
    {
        var claimId = await ClaimAssignedTo("priya");
        var client = await TestAuth.LoginAsync(_factory, "priya");

        var created = await client.PostAsJsonAsync($"/api/worklist/{claimId}/notes", new { text = "  Called payer, ref 77.  " });
        var detail = await Json(await client.GetAsync($"/api/worklist/{claimId}"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = Assert.Single(detail.GetProperty("notes").EnumerateArray());
        Assert.Equal(("priya", "Called payer, ref 77."), (note.GetProperty("author").GetString(), note.GetProperty("text").GetString()));
        Assert.Equal("NoteAdded", detail.GetProperty("audit").EnumerateArray().Last().GetProperty("action").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/worklist/{claimId}/notes", new { text = "   " })).StatusCode);
    }

    [Fact]
    public async Task Specialist_cannot_change_someone_elses_claim()
    {
        var karansClaim = await ClaimAssignedTo("karan");
        var client = await TestAuth.LoginAsync(_factory, "priya");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/worklist/{karansClaim}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/worklist/{karansClaim}/status", new { status = "Closed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/worklist/{karansClaim}/notes", new { text = "x" })).StatusCode);
    }

    [Fact]
    public async Task Manager_reassigns_and_specialist_cannot()
    {
        var claimId = await ClaimAssignedTo("karan");
        var manager = await TestAuth.LoginAsync(_factory, "manager");
        var specialist = await TestAuth.LoginAsync(_factory, "priya");

        var item = await Json(await manager.PutAsJsonAsync($"/api/worklist/{claimId}/assignee", new { username = "anjali" }));
        var detail = await Json(await manager.GetAsync($"/api/worklist/{claimId}"));

        Assert.Equal("anjali", item.GetProperty("assignedTo").GetString());
        Assert.Equal("Reassigned", detail.GetProperty("audit").EnumerateArray().Last().GetProperty("action").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync($"/api/worklist/{claimId}/assignee", new { username = "manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync($"/api/worklist/{claimId}/assignee", new { username = "ghost" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await specialist.PutAsJsonAsync($"/api/worklist/{claimId}/assignee", new { username = "priya" })).StatusCode);
        Assert.Equal(4, (await Json(await manager.GetAsync("/api/users"))).GetArrayLength());
    }

    [Fact]
    public async Task Closed_items_leave_the_default_queue()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        await client.PatchAsJsonAsync("/api/worklist/GPP-2026-001828/status", new { status = "Closed" });

        var open = await Json(await client.GetAsync("/api/worklist"));
        var all = await Json(await client.GetAsync("/api/worklist?status=all"));

        Assert.Equal(154, open.GetProperty("totalItems").GetInt32());
        Assert.Equal(155, all.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Reingestion_keeps_human_work()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        await client.PatchAsJsonAsync("/api/worklist/GPP-2026-000230/status", new { status = "PendingPayer" });
        await client.PutAsJsonAsync("/api/worklist/GPP-2026-000230/assignee", new { username = "rahul" });
        await client.PostAsJsonAsync("/api/worklist/GPP-2026-000230/notes", new { text = "Appeal sent." });

        await ReingestAsync();

        await using var db = pg.NewDb();
        var item = await db.WorkItems.SingleAsync(w => w.ClaimId == "GPP-2026-000230");
        Assert.Equal(("PendingPayer", "rahul"), (item.Status, item.AssignedTo));
        Assert.Equal(155, await db.WorkItems.CountAsync());
        Assert.Equal(1, await db.WorkNotes.CountAsync(n => n.ClaimId == "GPP-2026-000230"));
    }

    [Fact]
    public async Task Database_without_work_items_is_rebuilt_once_even_when_inputs_are_unchanged()
    {
        await using var db = pg.NewDb();
        await db.AuditEntries.ExecuteDeleteAsync();
        await db.WorkNotes.ExecuteDeleteAsync();
        await db.WorkItems.ExecuteDeleteAsync();
        var service = new IngestionService(db, new IngestionOptions(TestData.Dir, new DateOnly(2026, 9, 30)), NullLogger<IngestionService>.Instance);

        var first = await service.RunAsync();
        var workItemCount = await db.WorkItems.CountAsync();
        var second = await service.RunAsync();

        Assert.Equal(("Applied", 155, "NoChange"), (first.Outcome, workItemCount, second.Outcome));
    }

    [Fact]
    public async Task Unknown_status_filter_is_rejected()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/worklist?status=foo")).StatusCode);
    }

    [Fact]
    public async Task Pages_cover_every_claim_exactly_once()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");

        var claimIds = new List<string>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await Json(await client.GetAsync($"/api/worklist?page={page}&pageSize=50"));
            claimIds.AddRange(result.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("claimId").GetString()!));
        }

        Assert.Equal(155, claimIds.Count);
        Assert.Equal(155, claimIds.Distinct().Count());
        Assert.Equal("GPP-2026-001828", claimIds[0]);
    }

    [Fact]
    public async Task Search_matches_claim_patient_payer_and_root_cause()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        await using var db = pg.NewDb();
        var claim = await db.Claims.SingleAsync(c => c.ClaimId == "GPP-2026-001828");
        var payerErrors = await db.DenialAnalyses.CountAsync(a => a.RootCause == "Payer error");

        async Task<JsonElement> Search(string text) => await Json(await client.GetAsync($"/api/worklist?status=all&search={Uri.EscapeDataString(text)}"));

        var byClaim = await Search("001828");
        Assert.Equal(1, byClaim.GetProperty("totalItems").GetInt32());
        Assert.Equal(byClaim.GetProperty("items")[0].GetProperty("deniedAmount").GetDecimal(), byClaim.GetProperty("totalDenied").GetDecimal());
        var byPatient = await Search(claim.PatientLast.ToUpperInvariant());
        Assert.Contains("GPP-2026-001828", byPatient.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("claimId").GetString()));
        Assert.True((await Search(claim.Payer)).GetProperty("totalItems").GetInt32() > 1);
        Assert.Equal(payerErrors, (await Search("payer error")).GetProperty("totalItems").GetInt32());
        Assert.Equal(0, (await Search("%")).GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Sorting_by_a_column_works_in_both_directions()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        await using var db = pg.NewDb();
        var amounts = await db.DenialAnalyses.Select(a => a.DeniedAmount).ToListAsync();

        async Task<List<decimal>> Amounts(string direction) =>
            (await Json(await client.GetAsync($"/api/worklist?sort=deniedAmount&direction={direction}")))
                .GetProperty("items").EnumerateArray().Select(row => row.GetProperty("deniedAmount").GetDecimal()).ToList();

        var ascending = await Amounts("asc");
        var descending = await Amounts("desc");

        Assert.Equal((amounts.Min(), amounts.Max()), (ascending[0], descending[0]));
        Assert.Equal(ascending.Order(), ascending);
        Assert.Equal(descending.OrderDescending(), descending);
    }

    [Theory]
    [InlineData("sort=deadlineSoon")]
    [InlineData("direction=up")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=abc")]
    public async Task Invalid_grid_options_are_rejected(string query)
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/worklist?{query}")).StatusCode);
    }

    [Fact]
    public async Task Search_never_widens_a_specialists_view()
    {
        var karansClaim = await ClaimAssignedTo("karan");
        var client = await TestAuth.LoginAsync(_factory, "priya");
        await using var db = pg.NewDb();

        var everything = await Json(await client.GetAsync("/api/worklist?status=all&search=GPP&pageSize=100"));
        var someoneElses = await Json(await client.GetAsync($"/api/worklist?status=all&search={karansClaim}"));

        Assert.Equal(await db.WorkItems.CountAsync(w => w.AssignedTo == "priya"), everything.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, someoneElses.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Unchanged_status_and_assignee_write_no_audit_entry()
    {
        var claimId = await ClaimAssignedTo("priya", excludingStatus: "PendingPayer");
        var manager = await TestAuth.LoginAsync(_factory, "manager");

        await manager.PatchAsJsonAsync($"/api/worklist/{claimId}/status", new { status = "PendingPayer" });
        var repeatedStatus = await manager.PatchAsJsonAsync($"/api/worklist/{claimId}/status", new { status = "PendingPayer" });
        var repeatedAssignee = await manager.PutAsJsonAsync($"/api/worklist/{claimId}/assignee", new { username = "priya" });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (repeatedStatus.StatusCode, repeatedAssignee.StatusCode));
        await using var db = pg.NewDb();
        Assert.Equal(1, await db.AuditEntries.CountAsync(a => a.EntityId == claimId && a.Action == "StatusChanged"));
        Assert.Equal(0, await db.AuditEntries.CountAsync(a => a.EntityId == claimId && a.Action == "Reassigned"));
    }

    [Fact]
    public async Task A_stale_write_is_refused_instead_of_overwriting_a_reassignment()
    {
        var claimId = await ClaimAssignedTo("priya", excludingStatus: "PendingPayer");
        await using var specialistDb = pg.NewDb();
        await using var managerDb = pg.NewDb();
        var specialistCopy = await specialistDb.WorkItems.SingleAsync(w => w.ClaimId == claimId);
        var managerCopy = await managerDb.WorkItems.SingleAsync(w => w.ClaimId == claimId);

        managerCopy.AssignedTo = "karan";
        await managerDb.SaveChangesAsync();
        specialistCopy.Status = "PendingPayer";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => specialistDb.SaveChangesAsync());
    }

    [Fact]
    public async Task Errors_share_one_problem_details_shape()
    {
        var client = await TestAuth.LoginAsync(_factory, "priya");
        var forbidden = await client.GetAsync($"/api/worklist/{await ClaimAssignedTo("karan")}");
        var badFilter = await client.GetAsync("/api/worklist?status=Nope");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/problem+json", badFilter.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Manual_ingestion_run_is_audited_with_who_ran_it()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        var run = await Json(await client.PostAsync("/api/ingestion/run", null));

        await using var db = pg.NewDb();
        var runId = run.GetProperty("runId").GetInt32().ToString();
        Assert.True(await db.AuditEntries.AnyAsync(a => a.Entity == "Ingestion" && a.EntityId == runId && a.Actor == "manager"));
    }
}
