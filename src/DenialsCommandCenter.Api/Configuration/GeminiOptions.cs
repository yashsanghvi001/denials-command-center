using System.ComponentModel.DataAnnotations;

namespace DenialsCommandCenter.Api.Configuration;

public sealed class GeminiOptions : IValidatableObject
{
    public const string Section = "Gemini";

    // Secret: set only through the environment. Without it the rule-based template letters are used.
    public string? ApiKey { get; init; }

    [Required]
    public required Uri BaseUrl { get; init; }

    [Required]
    public required string Model { get; init; }

    // Zero turns model calls off; cached answers are still served. Nullable so that a missing key fails validation
    // instead of binding to zero and silently switching the AI off.
    [Required, Range(0, 100_000)]
    public required int? DailyCallLimit { get; init; }

    [Range(1, 300)]
    public required int TimeoutSeconds { get; init; }

    [Range(1, 3600)]
    public required int PauseAfterRefusalSeconds { get; init; }

    [Range(1, 1000)]
    public required int EvaluationStopAfterUnavailable { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        BaseUrlValidation.Check(BaseUrl, nameof(BaseUrl));
}
