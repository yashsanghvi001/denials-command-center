using DenialsCommandCenter.Api.Auth;

namespace DenialsCommandCenter.Tests.Auth;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("brighter-day-42")]
    [InlineData("abcdefg1")]
    public void Accepts_eight_characters_with_a_letter_and_a_number(string password) => Assert.Null(PasswordPolicy.Problem(password, TestOptions.Auth));

    [Theory]
    [InlineData(null)]
    [InlineData("abc1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Rejects_short_or_one_kind_passwords(string? password) => Assert.NotNull(PasswordPolicy.Problem(password, TestOptions.Auth));
}
