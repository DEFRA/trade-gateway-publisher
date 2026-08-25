using System.Text.Json;
using Trade.Gateway.Api.Contract.Events;

namespace Infrastructure;

public static class JsonExtensions
{
    public static string ToJson<T>(this T value)
    {
        return JsonSerializer.Serialize(value);
    }

    public static T? FromJson<T>(this string value)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(value);
    }
}
