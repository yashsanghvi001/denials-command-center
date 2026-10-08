using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Api.Auth;

public static class PasswordPolicy
{
    public static string? Problem(string? password, AuthOptions auth) => (password ?? "") switch
    {
        var candidate when candidate.Length < auth.PasswordMinLength => $"Use at least {auth.PasswordMinLength} characters.",
        var candidate when candidate.Length > auth.PasswordMaxLength => $"Use at most {auth.PasswordMaxLength} characters.",
        var candidate when !candidate.Any(char.IsLetter) || !candidate.Any(char.IsDigit) => "Use at least one letter and one number.",
        _ => null,
    };
}
