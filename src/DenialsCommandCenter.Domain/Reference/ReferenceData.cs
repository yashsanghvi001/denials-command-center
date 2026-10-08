namespace DenialsCommandCenter.Domain.Reference;

public sealed record ReferenceData(
    IReadOnlyDictionary<string, PayerRule> PayerRules, PolicyLibrary Policies, ReasonCodeReference ReasonCodes, IReadOnlyList<LabeledDenial> Labels);
