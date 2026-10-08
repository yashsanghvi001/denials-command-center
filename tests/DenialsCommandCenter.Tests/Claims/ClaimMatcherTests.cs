using DenialsCommandCenter.Domain.Claims;

namespace DenialsCommandCenter.Tests.Claims;

public class ClaimMatcherTests
{
    private readonly ClaimMatcher _matcher = new(["GPP-2026-000101", "GPP-2026-000230", "GPP-2025-000777", "GPP-2026-000777"]);

    [Theory]
    [InlineData("GPP-2026-000101")]
    [InlineData("GPP2026000101")]
    [InlineData("000101")]
    [InlineData(" gpp-2026-000101 ")]
    public void Matches_all_known_formats(string raw) => Assert.Equal("GPP-2026-000101", _matcher.Match(raw).ClaimId);

    [Fact]
    public void Other_practice_ids_do_not_match()
    {
        var result = _matcher.Match("BHC-2026-456493");
        Assert.Null(result.ClaimId);
        Assert.Contains("probably belongs to another practice", result.FailureReason);
    }

    [Fact]
    public void Unknown_gpp_id_does_not_match() => Assert.Contains("not in the claims export", _matcher.Match("GPP2026999999").FailureReason);

    [Fact]
    public void Bare_number_matching_two_claims_is_ambiguous()
    {
        var result = _matcher.Match("000777");
        Assert.Null(result.ClaimId);
        Assert.Contains("could be any of 2 claims", result.FailureReason);
    }
}
