using System.ComponentModel.DataAnnotations;

namespace DenialsCommandCenter.Api.Configuration;

public sealed class AuthOptions : IValidatableObject
{
    public const string Section = "Auth";

    // Secret: set only through the environment. Read once, when the users table is empty.
    public string? SeedPassword { get; init; }

    // Username to email address for password reset mail, set per deployment through Auth__Emails__{username}.
    public Dictionary<string, string> Emails { get; init; } = [];

    [Range(1, 24)]
    public required int SessionHours { get; init; }

    [Range(1, 300)]
    public required int SessionStampCacheSeconds { get; init; }

    [Range(5, 1440)]
    public required int PasswordResetMinutes { get; init; }

    [Range(6, 256)]
    public required int PasswordMinLength { get; init; }

    [Range(6, 256)]
    public required int PasswordMaxLength { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        PasswordMinLength < PasswordMaxLength
            ? []
            : [new ValidationResult($"{nameof(PasswordMinLength)} must be less than {nameof(PasswordMaxLength)}.",
                [nameof(PasswordMinLength), nameof(PasswordMaxLength)])];
}
