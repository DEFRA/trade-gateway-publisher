using System.Text.Json;
using TradeGatewayPublisher.Utils;

namespace TradeGatewayPublisher.Tests.Utils;

public class JsonExtensionsTests
{
    private sealed class Sample
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void FromJson_deserialises_valid_json()
    {
        const string json = "{\"Name\":\"example\"}";

        var value = json.FromJson<Sample>();

        Assert.Equal("example", value.Name);
    }

    [Fact]
    public void FromJson_throws_InvalidOperationException_when_json_is_null_literal()
    {
        const string json = "null";

        Assert.Throws<InvalidOperationException>(() => json.FromJson<Sample>());
    }

    [Fact]
    public void FromJson_throws_JsonException_when_json_is_invalid()
    {
        const string json = "{invalid-json}";

        Assert.Throws<JsonException>(() => json.FromJson<Sample>());
    }

    [Fact]
    public void ToJson_serialises_object()
    {
        var value = new Sample { Name = "example" };

        var json = value.ToJson();

        Assert.Contains("\"Name\":\"example\"", json);
    }
}
