using System.Reflection;
using Infrastructure.Messaging;
using Refit;

namespace Infrastructure.TracesGateway
{
    public interface ITracesGateway
    {
        [Get("/certificates/intras")]
        Task<FindIntraUpdatesResponse> FindIntraUpdates(
            DateTime updatedFrom,
            DateTime updatedBefore,
            int pageSize,
            int offset,
            CancellationToken cancellationToken
        );

        [Get("/certificates/intras/{id}")]
        Task<HttpResponseMessage> GetIntraCertification(string id, CancellationToken cancellationToken);

        [Get("/certificates/cheds")]
        Task<FindChedUpdatesResponse> FindChedUpdates(
            DateTime updatedFrom,
            DateTime updatedBefore,
            int pageSize,
            int offset,
            CancellationToken cancellationToken
        );

        [Get("/certificates/cheds/{id}")]
        Task<HttpResponseMessage> GetChedCertification(string id, CancellationToken cancellationToken);

        [Get("/health")]
        Task<HttpResponseMessage> HealthCheck(CancellationToken cancellationToken);
    }

    public record FindIntraUpdatesResponse(List<FindIntraUpdatesResponseRecord> Items);

    public record FindIntraUpdatesResponseRecord(string Id, DateTime Updated) : IMessage
    {
        public string DuplicationId { get; } = Guid.NewGuid().ToString("N");
    }

    public record FindChedUpdatesResponse(List<FindChedUpdatesResponseRecord> Items);

    public record FindChedUpdatesResponseRecord(string Id, DateTime Timestamp) : IMessage
    {
        public string DuplicationId { get; } = Id;
    }

    public class UtcDateTimeUrlParameterFormatter : IUrlParameterFormatter
    {
        public string? Format(object? value, ICustomAttributeProvider attributeProvider, Type type)
        {
            if (value is DateTime dt)
            {
                return dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            }

            return value?.ToString();
        }
    }
}
