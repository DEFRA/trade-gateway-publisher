using Defra.TradeImports.Tracing;

namespace Infrastructure.TracesGateway;

public class TracingDelegatingHandler(ITraceContextAccessor traceContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        request.Headers.Add(MetricNames.TraceKey, traceContextAccessor.Context?.TraceId);
        return await base.SendAsync(request, cancellationToken);
    }
}
