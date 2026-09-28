using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteDesktopClient.Core.Control;

internal static class ControlMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(ControlMessage message) => JsonSerializer.Serialize(message, Options);

    public static ControlMessage? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ControlMessage>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
