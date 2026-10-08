using System.ComponentModel.DataAnnotations;

namespace DenialsCommandCenter.Api.Configuration;

public sealed class AppOptions
{
    public const string Section = "App";

    // Where users open the web app; password reset links point here.
    [Required, Url]
    public required string PublicUrl { get; init; }
}
