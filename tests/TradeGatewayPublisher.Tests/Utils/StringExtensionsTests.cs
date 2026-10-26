using TradeGatewayPublisher.Utils;

namespace TradeGatewayPublisher.Tests.Utils;

public class StringExtensionsTests
{
    [Fact]
    public void GenerateHash_returns_expected_sha256_hex_for_known_input()
    {
        var hash = "hello".GenerateHash();

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
    }
}
