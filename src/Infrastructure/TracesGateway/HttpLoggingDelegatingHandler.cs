using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.TracesGateway;

public sealed class HttpLoggingDelegatingHandler(ILogger<HttpLoggingDelegatingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Sending HTTP {Method} {Uri}", request.Method, request.RequestUri);

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            stopwatch.Stop();

            logger.LogInformation(
                "Received HTTP {StatusCode} from {Method} {Uri} in {ElapsedMs}ms",
                (int)response.StatusCode,
                request.Method,
                request.RequestUri,
                stopwatch.ElapsedMilliseconds
            );

            return response;
        }
#pragma warning disable S2139
        catch (Exception ex)
#pragma warning restore S2139
        {
            stopwatch.Stop();

            logger.LogError(
                ex,
                "HTTP {Method} {Uri} failed after {ElapsedMs}ms",
                request.Method,
                request.RequestUri,
                stopwatch.ElapsedMilliseconds
            );

            throw;
        }
    }
}
