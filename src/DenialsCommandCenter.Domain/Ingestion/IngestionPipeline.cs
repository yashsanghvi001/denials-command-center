using System.Globalization;
using System.Text;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Worklog;
using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Domain.Ingestion;

public static class IngestionPipeline
{
    private const string WorklogSource = "denials_worklog.xlsx";

    private sealed record ParsedRemit(RemitSource Source, string Sha, Remittance Remittance);

    private sealed record RemitLedger(
        IReadOnlyList<RemitEvent> Events, IReadOnlyList<SourceFileSummary> Files,
        decimal TotalIncludingDuplicates, decimal UniqueTotal, decimal ProviderAdjustments);

    public static PipelineResult Run(PipelineInput input)
    {
        var issues = new List<IngestionIssue>();

        var (claims, claimIssues) = ClaimsCsvReader.Read(new StringReader(input.ClaimsCsv));
        issues.AddRange(claimIssues);
        var claimsById = claims.ToDictionary(c => c.ClaimId);
        var matcher = new ClaimMatcher(claimsById.Keys);

        var (remits, rejectedFiles) = ParseRemits(input.Remits, issues);
        var ledger = BuildEvents(remits, matcher, claimsById, issues);
        var states = ProjectStates(claims, ledger.Events, issues);
        var worklog = CheckWorklog(input, matcher, ledger.Events, states, issues);
        var report = BuildReconciliation(ledger, rejectedFiles, states);

        var finalIssues = issues
            .DistinctBy(i => i.Key)
            .OrderBy(i => i.Kind, StringComparer.Ordinal)
            .ThenBy(i => i.Source, StringComparer.Ordinal)
            .ThenBy(i => i.Reference, StringComparer.Ordinal)
            .ToList();
        return new PipelineResult(claims, ledger.Events, states, worklog, finalIssues, report);
    }

    // Byte-identical and unparseable files become issues; the rest are ordered by envelope so directory order never matters.
    private static (IReadOnlyList<ParsedRemit> Ordered, IReadOnlyList<SourceFileSummary> Rejected) ParseRemits(
        IReadOnlyList<RemitSource> sources, List<IngestionIssue> issues)
    {
        var parsed = new List<ParsedRemit>();
        var rejected = new List<SourceFileSummary>();
        var seenFileHashes = new HashSet<string>();
        foreach (var source in sources.OrderBy(r => r.FileName, StringComparer.Ordinal))
        {
            var sha = Hashing.Sha256Hex(source.Content);
            if (!seenFileHashes.Add(sha))
            {
                issues.Add(new(IssueKinds.DuplicateFile, Severity.Info, source.FileName, source.FileName, "This file is an exact copy of another remittance file, so it was skipped."));
                rejected.Add(new(-1, source.FileName, sha, null, null, 0, 0, 0, 0m, 0m, "Skipped: identical to another file"));
                continue;
            }
            try
            {
                parsed.Add(new(source, sha, X12Parser.Parse(Encoding.UTF8.GetString(source.Content))));
            }
            catch (Exception ex)
            {
                // A file dropped in for a live round must never stop the run, whatever the parser trips over.
                issues.Add(new(IssueKinds.UnparseableFile, Severity.Error, source.FileName, source.FileName, ex.Message));
                rejected.Add(new(-1, source.FileName, sha, null, null, 0, 0, 0, 0m, 0m, $"Rejected: {ex.Message}"));
            }
        }

        var ordered = parsed
            .OrderBy(p => p.Remittance.InterchangeDate)
            .ThenBy(p => p.Remittance.InterchangeControlNumber, StringComparer.Ordinal)
            .ThenBy(p => p.Source.FileName, StringComparer.Ordinal)
            .ToList();
        return (ordered, rejected.OrderBy(f => f.FileName, StringComparer.Ordinal).ToList());
    }

    // Events are deduplicated by content key, and each unique transaction's payment amount counts once.
    private static RemitLedger BuildEvents(
        IReadOnlyList<ParsedRemit> remits, ClaimMatcher matcher, IReadOnlyDictionary<string, ClaimRecord> claimsById, List<IngestionIssue> issues)
    {
        var seenEvents = new HashSet<string>();
        var seenTransactions = new HashSet<string>();
        var events = new List<RemitEvent>();
        var files = new List<SourceFileSummary>();
        decimal totalAll = 0m, totalUnique = 0m, providerAdjustments = 0m;

        for (int fileOrder = 0; fileOrder < remits.Count; fileOrder++)
        {
            var (source, sha, remittance) = remits[fileOrder];
            int count = 0, fresh = 0, duplicates = 0;
            decimal fileTotal = 0m, fileNewTotal = 0m;

            foreach (var transaction in remittance.Transactions)
            {
                fileTotal += transaction.PaymentAmount;
                var transactionIsNew = seenTransactions.Add(EventKey.ForTransaction(transaction));
                if (transactionIsNew)
                {
                    fileNewTotal += transaction.PaymentAmount;
                    providerAdjustments += transaction.ProviderAdjustments.Sum(p => p.Amount);
                    var imbalance = RemittanceValidator.TransactionImbalance(transaction);
                    if (imbalance != 0)
                        issues.Add(new(IssueKinds.TransactionOutOfBalance, Severity.Error, source.FileName, transaction.TraceNumber,
                            $"The payment of {FormatMoney(transaction.PaymentAmount)} is {FormatMoney(imbalance)} off from its claim payments less provider adjustments.",
                            Amount: imbalance));
                }

                int transactionFresh = 0, transactionDuplicates = 0;
                foreach (var claim in transaction.Claims)
                {
                    count++;
                    var key = EventKey.For(transaction.PayerId, transaction.TraceNumber, claim);
                    if (!seenEvents.Add(key)) { duplicates++; transactionDuplicates++; continue; }
                    fresh++; transactionFresh++;

                    // The key suffix keeps two different payments with the same claim reference from collapsing into one issue.
                    var reference = $"{transaction.TraceNumber}/{claim.RawClaimRef}/{claim.StatusCode}/{claim.FrequencyCode}/{key[..8]}";
                    var problem = RemittanceValidator.ClaimProblem(claim);
                    if (problem is not null)
                        issues.Add(new(IssueKinds.ClaimOutOfBalance, Severity.Error, source.FileName, reference, problem, Amount: claim.Paid));

                    var claimId = Resolve(claim, matcher, claimsById, source.FileName, reference, issues);
                    events.Add(new RemitEvent(key, claimId, transaction.PayerId, transaction.PayerName, transaction.TraceNumber,
                        transaction.PaymentDate, source.FileName, fileOrder, claim));
                }
                if (transactionFresh > 0 && transactionDuplicates > 0)
                    issues.Add(new(IssueKinds.PartialDuplicateTransaction, Severity.Error, source.FileName, transaction.TraceNumber,
                        $"Payment {transaction.TraceNumber} repeats {transactionDuplicates} claim payments we already loaded and adds {transactionFresh} new ones; "
                        + "its total is counted once, so it will not reconcile."));
            }

            totalAll += fileTotal;
            totalUnique += fileNewTotal;
            var allDuplicates = count > 0 && fresh == 0;
            if (allDuplicates)
            {
                var excluded = FormatMoney(fileTotal - fileNewTotal);
                var reason = fileNewTotal == 0
                    ? $"All {count} claim payments were already loaded from an earlier file, so {excluded} was not counted again."
                    : $"All {count} claim payments were already loaded from an earlier file; {excluded} excluded, but "
                      + $"{FormatMoney(fileNewTotal)} from payment totals not seen before was counted and will not reconcile.";
                issues.Add(new(IssueKinds.DuplicateRemittanceFile, Severity.Info, source.FileName, source.FileName, reason));
            }
            files.Add(new(fileOrder, source.FileName, sha, remittance.InterchangeControlNumber, remittance.InterchangeDate, count, fresh, duplicates,
                fileTotal, fileNewTotal, allDuplicates ? "Duplicate of earlier file" : "Loaded"));
        }

        return new RemitLedger(events, files, totalAll, totalUnique, providerAdjustments);
    }

    private static IReadOnlyList<ClaimState> ProjectStates(
        IReadOnlyList<ClaimRecord> claims, IReadOnlyList<RemitEvent> events, List<IngestionIssue> issues)
    {
        var eventsByClaim = events.Where(e => e.ClaimId is not null).ToLookup(e => e.ClaimId!);
        var states = claims.Select(c => ClaimStateProjector.Project(c.ClaimId, c.TotalCharge, eventsByClaim[c.ClaimId])).ToList();
        foreach (var state in states)
        {
            if (state.PossibleOverpayment > 0)
                issues.Add(new(IssueKinds.PossibleOverpayment, Severity.Warning, "remits", state.ClaimId,
                    $"The payer has paid {FormatMoney(state.NetPaid)} in total, but its latest decision on this claim is {FormatMoney(state.NetPaid - state.PossibleOverpayment)}. "
                    + $"Expect the payer to take back {FormatMoney(state.PossibleOverpayment)}.",
                    state.ClaimId, state.PossibleOverpayment));
            if (state.UnmatchedReversals > 0)
                issues.Add(new(IssueKinds.UnmatchedReversal, Severity.Warning, "remits", state.ClaimId,
                    $"{state.UnmatchedReversals} payer reversal(s) do not match any earlier payment with the same payer claim number, "
                    + "so they lower the paid total without cancelling a payment.",
                    state.ClaimId));
            if (state.Status == ClaimStatus.Unknown)
                issues.Add(new(IssueKinds.UnknownClaimStatus, Severity.Error, "remits", state.ClaimId,
                    "The latest remittance uses a claim status code we do not recognize, so the claim needs a manual check.", state.ClaimId));
        }
        return states;
    }

    // The worklog is the team's own tracker, so it is checked against the payer's truth rather than trusted.
    private static IReadOnlyList<WorklogEntry> CheckWorklog(
        PipelineInput input, ClaimMatcher matcher, IReadOnlyList<RemitEvent> events, IReadOnlyList<ClaimState> states, List<IngestionIssue> issues)
    {
        var firstRemit = events
            .Where(e => e.ClaimId is not null)
            .GroupBy(e => e.ClaimId!)
            .ToDictionary(g => g.Key, g => g.Min(e => e.PaymentDate));
        var (worklog, worklogIssues) = WorklogNormalizer.Normalize(input.WorklogRows, matcher, firstRemit, input.Today, WorklogSource);
        issues.AddRange(worklogIssues);

        var stateById = states.ToDictionary(s => s.ClaimId);
        foreach (var entry in worklog.Where(w => w.ClaimId is not null))
        {
            var state = stateById[entry.ClaimId!];
            var reference = $"row {entry.RowNumber}";
            var denied = state.Status is ClaimStatus.Denied or ClaimStatus.PartiallyDenied;
            if (entry.Status == WorklogStatus.Closed && denied)
                issues.Add(new(IssueKinds.WorklogClosedButStillDenied, Severity.Warning, WorklogSource, reference,
                    $"The worklog marks this closed, but the payer still denies {FormatMoney(state.DeniedAmount)}.", state.ClaimId, state.DeniedAmount));
            if (entry.Status != WorklogStatus.Closed && state.Status == ClaimStatus.Paid)
                issues.Add(new(IssueKinds.WorklogOpenButPaid, Severity.Info, WorklogSource, reference,
                    "The worklog item is still open, but the claim is now paid in full.", state.ClaimId));
            if (denied && entry.Amount is { } amount && amount != state.DeniedAmount)
                issues.Add(new(IssueKinds.WorklogAmountMismatch, Severity.Info, WorklogSource, reference,
                    $"The worklog says {FormatMoney(amount)}, but the payer denied {FormatMoney(state.DeniedAmount)}.", state.ClaimId));
        }
        return worklog;
    }

    private static ReconciliationReport BuildReconciliation(
        RemitLedger ledger, IReadOnlyList<SourceFileSummary> rejectedFiles, IReadOnlyList<ClaimState> states)
    {
        var paidKnown = ledger.Events.Where(e => e.ClaimId is not null).Sum(e => e.Payment.Paid);
        var paidUnmatched = ledger.Events.Where(e => e.ClaimId is null).Sum(e => e.Payment.Paid);
        return new ReconciliationReport(
            ledger.TotalIncludingDuplicates, ledger.TotalIncludingDuplicates - ledger.UniqueTotal, ledger.UniqueTotal,
            paidKnown, paidUnmatched, ledger.ProviderAdjustments,
            ledger.UniqueTotal - (paidKnown + paidUnmatched - ledger.ProviderAdjustments),
            Enum.GetValues<ClaimStatus>().ToDictionary(s => s.ToString(), s => states.Count(x => x.Status == s)),
            [.. ledger.Files, .. rejectedFiles]);
    }

    private static string? Resolve(ClaimPayment claim, ClaimMatcher matcher, IReadOnlyDictionary<string, ClaimRecord> claims,
        string file, string reference, List<IngestionIssue> issues)
    {
        var match = matcher.Match(claim.RawClaimRef);
        if (match.ClaimId is null)
        {
            issues.Add(new(IssueKinds.UnmatchedRemittance, Severity.Error, file, reference, match.FailureReason!, Amount: claim.Paid));
            return null;
        }
        if (!string.Equals(claims[match.ClaimId].MemberId, claim.MemberId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(IssueKinds.MemberMismatch, Severity.Error, file, reference,
                $"The patient on this payment does not match claim {match.ClaimId}, so the payment was not applied.", match.ClaimId, claim.Paid));
            return null;
        }
        return match.ClaimId;
    }

    private static string FormatMoney(decimal amount) => amount.ToString("$#,##0.00;-$#,##0.00", CultureInfo.InvariantCulture);
}
