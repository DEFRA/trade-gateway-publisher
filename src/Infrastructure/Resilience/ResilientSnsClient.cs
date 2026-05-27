using System.Diagnostics.CodeAnalysis;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Resilience;

[ExcludeFromCodeCoverage]
public sealed class ResilientSnsClient : AmazonSimpleNotificationServiceClient
{
    private readonly ResilientSnsClientRetryHandler _retryHandler;

    public ResilientSnsClient(ILogger<ResilientSnsClient> logger)
    {
        _retryHandler = new ResilientSnsClientRetryHandler(logger);
    }

    public ResilientSnsClient(
        ILogger<ResilientSnsClient> logger,
        AWSCredentials credentials,
        AmazonSimpleNotificationServiceConfig config
    )
        : base(credentials, config)
    {
        _retryHandler = new ResilientSnsClientRetryHandler(logger);
    }

    public override Task<PublishBatchResponse> PublishBatchAsync(
        PublishBatchRequest request,
        CancellationToken cancellationToken = default
    ) => _retryHandler.PublishWithRetryAsync(base.PublishBatchAsync, request, cancellationToken);
}
