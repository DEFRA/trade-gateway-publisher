using Infrastructure.Messaging;
using Refit;

namespace Infrastructure.TracesGateway
{
    public interface ITracesGateway
    {
        [Get("/intra/find")]
        Task<FindIntraUpdatesResponse> FindIntraUpdates(
            DateTime start,
            DateTime end,
            int pageSize,
            int offset,
            CancellationToken cancellationToken
        );

        [Get("/intra/{id}")]
        Task<HttpResponseMessage> GetIntraCertification(string id, CancellationToken cancellationToken);

        [Get("/ched/find")]
        Task<FindChedUpdatesResponse> FindChedUpdates(
            DateTime start,
            DateTime end,
            int pageSize,
            int offset,
            CancellationToken cancellationToken
        );

        [Get("/ched/{id}")]
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
