using System.Globalization;

namespace DenialsCommandCenter.Domain.X12;

public static class X12Parser
{
    private const int IsaLength = 106;

    public static Remittance Parse(string content)
    {
        content = content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (content.Length < IsaLength || !content.StartsWith("ISA", StringComparison.Ordinal))
            throw new X12FormatException("File does not start with a 106-character ISA segment.");

        char elementSep = content[3];
        char componentSep = content[104];
        char segmentTerm = content[105];

        var segments = content.Split(segmentTerm)
            .Select(s => s.Trim(' ', '\t', '\r', '\n'))
            .Where(s => s.Length > 0)
            .Select(s => s.Split(elementSep))
            .ToList();

        var isa = segments[0];
        var controlNumber = Get(isa, 13).Trim();
        var interchangeDate = ParseDate(Get(isa, 9), "yyMMdd", 0);

        var transactions = new List<RemittanceTransaction>();
        TransactionBuilder? transaction = null;
        ClaimBuilder? claim = null;
        ServiceBuilder? service = null;

        for (int i = 1; i < segments.Count; i++)
        {
            var elements = segments[i];
            switch (elements[0])
            {
                case "ST":
                    if (transaction is not null) throw new X12FormatException($"Segment {i}: ST found before the previous SE.");
                    transaction = new TransactionBuilder();
                    claim = null;
                    service = null;
                    break;
                case "BPR":
                    InTransaction(transaction, i).PaymentAmount = ParseDecimal(Get(elements, 2), i);
                    transaction!.PaymentDate = ParseDate(Get(elements, 16), "yyyyMMdd", i);
                    break;
                case "TRN":
                    InTransaction(transaction, i).TraceNumber = Get(elements, 2);
                    break;
                case "N1" when Get(elements, 1) == "PR":
                    InTransaction(transaction, i).PayerName = Get(elements, 2);
                    break;
                case "REF" when Get(elements, 1) == "2U" && claim is null:
                    InTransaction(transaction, i).PayerId = Get(elements, 2);
                    break;
                case "CLP":
                    claim = new ClaimBuilder
                    {
                        Sequence = i,
                        RawClaimRef = Get(elements, 1),
                        StatusCode = Get(elements, 2),
                        Charge = ParseDecimal(Get(elements, 3), i),
                        Paid = ParseDecimal(Get(elements, 4), i),
                        PatientResponsibility = ParseOptionalDecimal(Get(elements, 5), i),
                        PayerClaimControlNumber = Get(elements, 7),
                        FrequencyCode = Get(elements, 9),
                    };
                    InTransaction(transaction, i).Claims.Add(claim);
                    service = null;
                    break;
                case "NM1" when claim is not null && Get(elements, 1) == "QC":
                    claim.PatientLastName = Get(elements, 3);
                    claim.PatientFirstName = Get(elements, 4);
                    claim.MemberId = Get(elements, 9);
                    break;
                case "NM1" when claim is not null && Get(elements, 1) == "82":
                    claim.RenderingNpi = Get(elements, 9);
                    break;
                case "DTM" when service is not null && Get(elements, 1) == "472":
                    service.ServiceDate = ParseDate(Get(elements, 2), "yyyyMMdd", i);
                    break;
                case "DTM" when service is null && claim is not null && Get(elements, 1) == "050":
                    claim.ReceivedDate = ParseDate(Get(elements, 2), "yyyyMMdd", i);
                    break;
                case "SVC":
                {
                    var procedure = Get(elements, 1).Split(componentSep);
                    service = new ServiceBuilder
                    {
                        ProcedureCode = procedure.Length > 1 ? procedure[1] : procedure[0],
                        Modifiers = procedure.Skip(2).Where(m => m.Length > 0).ToList(),
                        Charge = ParseDecimal(Get(elements, 2), i),
                        Paid = ParseDecimal(Get(elements, 3), i),
                        Units = Get(elements, 5).Length > 0 ? ParseDecimal(Get(elements, 5), i) : 1m,
                    };
                    InClaim(claim, i).Services.Add(service);
                    break;
                }
                case "CAS":
                {
                    var adjustments = ParseCas(elements, i).ToList();
                    if (service is not null) service.Adjustments.AddRange(adjustments);
                    else InClaim(claim, i).Adjustments.AddRange(adjustments);
                    break;
                }
                case "LQ" when Get(elements, 1) == "HE":
                    if (service is not null) service.Remarks.Add(Get(elements, 2));
                    else InClaim(claim, i).Remarks.Add(Get(elements, 2));
                    break;
                case "PLB":
                    InTransaction(transaction, i).ProviderAdjustments.AddRange(ParsePlb(elements, componentSep, i));
                    claim = null;
                    service = null;
                    break;
                case "SE":
                    transactions.Add(InTransaction(transaction, i).Build(i));
                    transaction = null;
                    claim = null;
                    service = null;
                    break;
            }
        }

        if (transaction is not null) throw new X12FormatException("Transaction set is missing its SE trailer.");
        return new Remittance(controlNumber, interchangeDate, transactions);
    }

    private static IEnumerable<Adjustment> ParseCas(string[] elements, int segmentNumber)
    {
        var group = Get(elements, 1);
        for (int k = 2; k + 1 < elements.Length; k += 3)
        {
            if (elements[k].Length == 0) continue;
            yield return new Adjustment(group, elements[k], ParseDecimal(elements[k + 1], segmentNumber));
        }
    }

    private static IEnumerable<ProviderAdjustment> ParsePlb(string[] elements, char componentSep, int segmentNumber)
    {
        for (int k = 3; k + 1 < elements.Length; k += 2)
        {
            if (elements[k].Length == 0) continue;
            var identifier = elements[k].Split(componentSep);
            yield return new ProviderAdjustment(identifier[0], identifier.Length > 1 ? identifier[1] : "", ParseDecimal(elements[k + 1], segmentNumber));
        }
    }

    private static string Get(string[] elements, int index) => index < elements.Length ? elements[index] : "";

    private static decimal ParseDecimal(string text, int segmentNumber) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new X12FormatException($"Segment {segmentNumber}: '{text}' is not a valid amount.");

    private static decimal ParseOptionalDecimal(string text, int segmentNumber) => text.Length == 0 ? 0m : ParseDecimal(text, segmentNumber);

    private static DateOnly ParseDate(string text, string format, int segmentNumber) =>
        DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new X12FormatException($"Segment {segmentNumber}: '{text}' is not a valid date ({format}).");

    private static TransactionBuilder InTransaction(TransactionBuilder? transaction, int segmentNumber) =>
        transaction ?? throw new X12FormatException($"Segment {segmentNumber}: found outside an ST/SE transaction.");

    private static ClaimBuilder InClaim(ClaimBuilder? claim, int segmentNumber) =>
        claim ?? throw new X12FormatException($"Segment {segmentNumber}: found outside a CLP claim loop.");

    private sealed class TransactionBuilder
    {
        public string PayerName { get; set; } = "";
        public string PayerId { get; set; } = "";
        public string TraceNumber { get; set; } = "";
        public decimal PaymentAmount { get; set; }
        public DateOnly? PaymentDate { get; set; }
        public List<ClaimBuilder> Claims { get; } = [];
        public List<ProviderAdjustment> ProviderAdjustments { get; } = [];

        public RemittanceTransaction Build(int segmentNumber) => new(
            PayerName, PayerId, TraceNumber, PaymentAmount,
            PaymentDate ?? throw new X12FormatException($"Segment {segmentNumber}: transaction has no BPR payment date."),
            Claims.Select(c => c.Build()).ToList(), ProviderAdjustments.ToList());
    }

    private sealed class ClaimBuilder
    {
        public int Sequence { get; init; }
        public string RawClaimRef { get; init; } = "";
        public string StatusCode { get; init; } = "";
        public decimal Charge { get; init; }
        public decimal Paid { get; init; }
        public decimal PatientResponsibility { get; init; }
        public string PayerClaimControlNumber { get; init; } = "";
        public string FrequencyCode { get; init; } = "";
        public string PatientLastName { get; set; } = "";
        public string PatientFirstName { get; set; } = "";
        public string MemberId { get; set; } = "";
        public string RenderingNpi { get; set; } = "";
        public DateOnly? ReceivedDate { get; set; }
        public List<Adjustment> Adjustments { get; } = [];
        public List<string> Remarks { get; } = [];
        public List<ServiceBuilder> Services { get; } = [];

        public ClaimPayment Build() => new(
            Sequence, RawClaimRef, StatusCode, Charge, Paid, PatientResponsibility, PayerClaimControlNumber, FrequencyCode,
            PatientLastName, PatientFirstName, MemberId, RenderingNpi, ReceivedDate,
            Adjustments.ToList(), Remarks.ToList(), Services.Select(s => s.Build()).ToList());
    }

    private sealed class ServiceBuilder
    {
        public string ProcedureCode { get; init; } = "";
        public List<string> Modifiers { get; init; } = [];
        public decimal Charge { get; init; }
        public decimal Paid { get; init; }
        public decimal Units { get; init; }
        public DateOnly? ServiceDate { get; set; }
        public List<Adjustment> Adjustments { get; } = [];
        public List<string> Remarks { get; } = [];

        public ServicePayment Build() => new(ProcedureCode, Modifiers.ToList(), Charge, Paid, Units, ServiceDate, Adjustments.ToList(), Remarks.ToList());
    }
}
