using Infrastructure.Messaging;
using Refit;

namespace Infrastructure.TracesGateway
{
    public interface ITracesGateway
    {
        [Get("/intras")]
        Task<FindIntraUpdatesResponse> FindIntraUpdates(
            DateTime updatedFrom,
            DateTime updatedBefore,
            int pageSize,
            int offset,
            CancellationToken cancellationToken
        );

        [Get("/intras/{id}")]
        Task<HttpResponseMessage> GetIntraCertification(string id, CancellationToken cancellationToken);

        [Get("/cheds")]
        Task<FindChedUpdatesResponse> FindChedUpdates(
            DateTime updatedFrom,
            DateTime updatedBefore,
            int pageSize,
            int offset,
            CancellationToken cancellationToken
        );

        [Get("/cheds/{id}")]
        Task<HttpResponseMessage> GetChedCertification(string id, CancellationToken cancellationToken);
    }

    public record FindIntraUpdatesResponse(List<FindIntraUpdatesResponseRecord> Data);

    public record FindIntraUpdatesResponseRecord(string Id, DateTime Timestamp) : IMessage
    {
        public string DuplicationId { get; } = Id;
    }

    public record FindChedUpdatesResponse(List<FindChedUpdatesResponseRecord> Data);

    public record FindChedUpdatesResponseRecord(string Id, DateTime Timestamp) : IMessage
    {
        public string DuplicationId { get; } = Id;
    }
}
