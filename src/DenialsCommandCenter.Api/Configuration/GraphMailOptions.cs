using System.ComponentModel.DataAnnotations;

namespace DenialsCommandCenter.Api.Configuration;

// The app registration and sender are per deployment and secret, so they come only from the environment.
// Password reset by email is offered only when all four are set.
public sealed class GraphMailOptions : IValidatableObject
{
    public const string Section = "Graph";

    public string? TenantId { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public string? SenderAddress { get; init; }

    [Required]
    public required Uri AuthorityUrl { get; init; }

    [Required]
    public required Uri ApiBaseUrl { get; init; }

    [Required]
    public required string Scope { get; init; }

    [Range(1, 300)]
    public required int TimeoutSeconds { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(SenderAddress);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        [.. BaseUrlValidation.Check(AuthorityUrl, nameof(AuthorityUrl)), .. BaseUrlValidation.Check(ApiBaseUrl, nameof(ApiBaseUrl))];
}
