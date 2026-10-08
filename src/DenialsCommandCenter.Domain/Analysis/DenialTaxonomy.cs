namespace DenialsCommandCenter.Domain.Analysis;

public static class RootCauses
{
    public const string CodingDiagnosis = "Coding - diagnosis";
    public const string CodingModifier = "Coding - modifier";
    public const string CodingFrequency = "Coding - frequency";
    public const string PayerError = "Payer error";
    public const string Authorization = "Authorization";
    public const string Credentialing = "Credentialing";
    public const string Eligibility = "Eligibility";
    public const string TimelyFiling = "Billing - timely filing";
    public const string Duplicate = "Billing - duplicate";
    public const string MedicalNecessity = "Medical necessity";
    public const string Unclassified = "Unclassified";

    public static readonly IReadOnlyList<string> All =
    [
        CodingDiagnosis, CodingModifier, CodingFrequency, PayerError, Authorization, Credentialing,
        Eligibility, TimelyFiling, Duplicate, MedicalNecessity, Unclassified,
    ];
}

public static class OwningTeams
{
    public const string Coding = "Coding";
    public const string Clinical = "Coding / Clinical";
    public const string DenialsAppeal = "Denials (appeal)";
    public const string Authorization = "Front desk / Authorization";
    public const string Eligibility = "Front desk / Eligibility";
    public const string Credentialing = "Credentialing";
    public const string Billing = "Billing";

    public static readonly IReadOnlyList<string> All = [Coding, Clinical, DenialsAppeal, Authorization, Eligibility, Credentialing, Billing];
}

public static class Preventability
{
    public const string Yes = "Yes";
    public const string No = "No";
    public const string Unknown = "Unknown";

    public static readonly IReadOnlyList<string> All = [Yes, No, Unknown];
}

public enum NextActionType { CorrectedClaim, Appeal, RequestRetroAuthorization, BillOtherCoverage, WriteOff, CloseAsDuplicate, HumanReview }

public enum Confidence { High, Low }
