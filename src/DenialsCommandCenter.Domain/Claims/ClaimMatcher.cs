using System.Text.RegularExpressions;

namespace DenialsCommandCenter.Domain.Claims;

public sealed record MatchResult(string? ClaimId, string? FailureReason);

public sealed class ClaimMatcher
{
    private static readonly Regex Full = new(@"^GPP-?(\d{4})-?(\d{6})$", RegexOptions.CultureInvariant);
    private static readonly Regex Bare = new(@"^\d{6}$", RegexOptions.CultureInvariant);

    private readonly HashSet<string> _known;
    private readonly ILookup<string, string> _bySuffix;

    public ClaimMatcher(IEnumerable<string> knownClaimIds)
    {
        _known = knownClaimIds.ToHashSet(StringComparer.Ordinal);
        _bySuffix = _known.Where(id => id.Length >= 6).ToLookup(id => id[^6..]);
    }

    public MatchResult Match(string raw)
    {
        var normalized = raw.Trim().ToUpperInvariant();

        var full = Full.Match(normalized);
        if (full.Success)
        {
            var id = $"GPP-{full.Groups[1].Value}-{full.Groups[2].Value}";
            return _known.Contains(id)
                ? new(id, null)
                : new(null, $"Claim number '{raw}' looks like {id}, but that claim is not in the claims export.");
        }

        if (Bare.IsMatch(normalized))
        {
            var candidates = _bySuffix[normalized].OrderBy(x => x, StringComparer.Ordinal).ToList();
            return candidates.Count switch
            {
                1 => new(candidates[0], null),
                0 => new(null, $"Claim number '{raw}' does not match any claim in the claims export."),
                _ => new(null, $"Claim number '{raw}' could be any of {candidates.Count} claims ({string.Join(", ", candidates)}), so it was not applied."),
            };
        }

        return new(null, $"Claim number '{raw}' is not in Gulfview's format, so this payment probably belongs to another practice.");
    }
}
