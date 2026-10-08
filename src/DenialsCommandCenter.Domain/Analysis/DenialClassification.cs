using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Analysis;

public sealed record DenialClassification(
    string RootCause, string OwningTeam, string Preventable, NextActionType Action, string NextAction,
    IReadOnlyList<string> Citations, Confidence Confidence, string? ConfidenceReason, IReadOnlyList<string> ReasonCodes);

public sealed record DenialContext(
    ClaimRecord Claim, ClaimState State, PayerRule? PayerRule, IReadOnlyList<ClaimRecord> SameDayClaimsForPatient, DateOnly Today);
