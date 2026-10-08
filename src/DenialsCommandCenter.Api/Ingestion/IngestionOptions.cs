using System.Globalization;

namespace DenialsCommandCenter.Api.Ingestion;

public sealed record IngestionOptions(string DataDir, DateOnly Today)
{
    public static IngestionOptions FromConfiguration(IConfiguration config) => new(
        Required(config, "Ingestion:DataDirectory"),
        DateOnly.ParseExact(Required(config, "Ingestion:Today"), "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static string Required(IConfiguration config, string key) =>
        config[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{key} is not set; add it to appsettings.json or set {key.Replace(":", "__")}.");
}
