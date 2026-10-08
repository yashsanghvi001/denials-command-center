using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenialsCommandCenter.Domain;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
