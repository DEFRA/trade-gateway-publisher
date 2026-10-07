using System.Text.Json;
using Infrastructure;
using Infrastructure.Messaging.Consuming;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using Trade.Gateway.Api.Contract.Events;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ChedChanges
{
    public class ChedUpdateConsumer(
        ITracesGatewayChedClient tracesGateway,
        ISnsPublisher snsPublisher,
        IOptions<TracesUpdatePublisherOptions> options,
        ILogger<ChedUpdateConsumer> logger
    ) : IMessageConsumer
    {
        // UN/CEFACT 1001 "Partial (Partially Rejected)"
        private const string PartiallyRejectedStatusCode = "237";

        // UN/CEFACT 1001 "Call-off delivery" - TRACES uses this for the split R (rejected) and V (validated) CHEDs
        private const string SplitChedTypeCode = "75";

        // UN/CEFACT 1153 "Related document number"
        private const string RelatedDocumentRelationshipCode = "57";

        public async Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default)
        {
            var message = JsonSerializer.Deserialize<DefraUNVTDCHEDSummaryProfileItem>(context.Body);

            var apiResponse = await tracesGateway.GetChedCertification(message!.Id, cancellationToken);
            var certificate = apiResponse.Content;

            // Placeholder deduplication id — see "Message Deduplication" in README.md
            await PublishAsync(certificate!, message.GetDuplicationId(), context, cancellationToken);

            await ProcessLinkedCheds(context, certificate, cancellationToken);
        }

        private async Task ProcessLinkedCheds(
            MessageContext context,
            DefraUNVTDCHEDProfile? certificate,
            CancellationToken cancellationToken
        )
        {
            foreach (var linkedId in GetLinkedChedIds(certificate!))
            {
                var linkedResponse = await tracesGateway.GetChedCertification(linkedId, cancellationToken);
                await linkedResponse.EnsureSuccessfulAsync();

                await PublishAsync(
                    linkedResponse.Content!,
                    linkedResponse.GetDuplicationId(),
                    context,
                    cancellationToken
                );
            }
        }

        private async Task PublishAsync(
            DefraUNVTDCHEDProfile certificate,
            string duplicationId,
            MessageContext context,
            CancellationToken cancellationToken
        )
        {
            var @event = certificate.ToEventEnvelope(context.GetTraceId());

            await snsPublisher.PublishAsync(
                options.Value.ChedTopicArn,
                JsonSerializer.Serialize(@event),
                duplicationId: duplicationId,
                cancellationToken: cancellationToken
            );
            logger.LogInformation(
                "Published CHED event {Id} to SNS ARN {Topic}",
                @event.EventId,
                options.Value.ChedTopicArn
            );
        }

        private static IEnumerable<string> GetLinkedChedIds(DefraUNVTDCHEDProfile certificate)
        {
            var document = certificate.ExchangedDocument;

            if (document?.DocumentStatusCode != PartiallyRejectedStatusCode)
                return [];

            return (document.ReferenceDocument ?? [])
                .Where(reference =>
                    reference is { TypeCode: SplitChedTypeCode, RelationshipTypeCode: RelatedDocumentRelationshipCode }
                    && !string.IsNullOrWhiteSpace(reference.Identifier)
                    && reference.Identifier != document.Identifier
                )
                .Select(reference => reference.Identifier!)
                .Distinct();
        }
    }
}
