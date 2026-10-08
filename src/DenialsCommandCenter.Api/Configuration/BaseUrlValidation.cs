using System.ComponentModel.DataAnnotations;

namespace DenialsCommandCenter.Api.Configuration;

internal static class BaseUrlValidation
{
    // Request paths are resolved against these URLs, which keeps the whole base path only when it ends in "/".
    public static IEnumerable<ValidationResult> Check(Uri? url, string name) =>
        url is { IsAbsoluteUri: true } && url.AbsolutePath.EndsWith('/')
            ? []
            : [new ValidationResult($"{name} must be an absolute URL ending in /.", [name])];
}
