using System.Text.Json;

namespace TradeGatewayPublisher.Utils
{
    public static class JsonExtensions
    {
        public static T FromJson<T>(this string serialisedObject)
        {
            return JsonSerializer.Deserialize<T>(serialisedObject)
                ?? throw new InvalidOperationException("Cannot deserialise object");
        }

        public static string ToJson<T>(this T objectToSerialise)
        {
            return JsonSerializer.Serialize(objectToSerialise)
                ?? throw new InvalidOperationException("Cannot serialise object");
        }
    }
}
