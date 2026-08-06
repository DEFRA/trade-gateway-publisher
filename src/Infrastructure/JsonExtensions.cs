using System.Text.Json;

namespace Infrastructure;

public static class JsonExtensions
{
    public static string ToJson<T>(this T value)
    {
        return JsonSerializer.Serialize(value);
    }
}