using System.Security.Cryptography;
using System.Text;

namespace TradeGatewayPublisher.Utils
{
    public static class StringExtensions
    {
        public static string GenerateHash(this string subject)
        {
            var data = SHA256.HashData(Encoding.UTF8.GetBytes(subject));

            var sBuilder = new StringBuilder();

            foreach (var hashByte in data)
            {
                sBuilder.Append(hashByte.ToString("x2"));
            }

            return sBuilder.ToString();
        }
    }
}
