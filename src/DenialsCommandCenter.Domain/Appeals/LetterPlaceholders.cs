using System.Globalization;
using DenialsCommandCenter.Domain.Claims;

namespace DenialsCommandCenter.Domain.Appeals;

public static class LetterPlaceholders
{
    public const string PatientName = "{{PATIENT_NAME}}";
    public const string MemberId = "{{MEMBER_ID}}";
    public const string ClaimId = "{{CLAIM_ID}}";
    public const string DateOfService = "{{DATE_OF_SERVICE}}";
    public const string DenialDate = "{{DENIAL_DATE}}";
    public const string ProviderName = "{{PROVIDER_NAME}}";
    public const string ProviderNpi = "{{PROVIDER_NPI}}";

    public static readonly IReadOnlyList<string> All = [PatientName, MemberId, ClaimId, DateOfService, DenialDate, ProviderName, ProviderNpi];

    public static string Merge(string template, ClaimRecord claim, DateOnly? denialDate) => template
        .Replace(PatientName, $"{claim.PatientFirst} {claim.PatientLast}")
        .Replace(MemberId, claim.MemberId)
        .Replace(ClaimId, claim.ClaimId)
        .Replace(DateOfService, FormatDate(claim.DateOfService))
        .Replace(DenialDate, denialDate is { } date ? FormatDate(date) : "")
        .Replace(ProviderName, claim.RenderingProvider)
        .Replace(ProviderNpi, claim.RenderingNpi);

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
