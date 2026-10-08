using System.ComponentModel.DataAnnotations;

namespace DenialsCommandCenter.Api.Configuration;

// Protections against abuse and overload. The ranges are strict on purpose: a typo such as a permit limit of 0
// must stop startup rather than quietly lock everyone out or switch a protection off.
public sealed class LimitsOptions : IValidatableObject
{
    public const string Section = "Limits";

    [Range(1, 1000)]
    public required int LoginAttemptsPerMinute { get; init; }

    [Range(1, 100)]
    public required int ExpensiveConcurrentRequests { get; init; }

    // Zero is valid (no waiting line), so the property is nullable: a missing key must fail validation, not bind to zero.
    [Required, Range(0, 10_000)]
    public required int? ExpensiveQueueLength { get; init; }

    [Range(1024, 10 * 1024 * 1024)]
    public required int MaxRequestBodyBytes { get; init; }

    [Range(1, 1000)]
    public required int DefaultPageSize { get; init; }

    [Range(1, 1000)]
    public required int MaxPageSize { get; init; }

    [Range(1, 100_000)]
    public required int MaxNoteLength { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DefaultPageSize <= MaxPageSize
            ? []
            : [new ValidationResult($"{nameof(DefaultPageSize)} must not be greater than {nameof(MaxPageSize)}.",
                [nameof(DefaultPageSize), nameof(MaxPageSize)])];
}
