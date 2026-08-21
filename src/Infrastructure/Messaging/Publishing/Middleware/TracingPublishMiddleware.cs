using Defra.TradeImports.Tracing;
using Infrastructure.Messaging.Extensions;

namespace Infrastructure.Messaging.Publishing.Middleware;

public class TracingPublishMiddleware(ITraceContextAccessor traceContextAccessor) : IPublishMiddleware
{
    public Task InvokeAsync(PublishContext context, Func<Task> next, CancellationToken cancellationToken = default)
    {
        context.SetTraceId(traceContextAccessor.Context?.TraceId);
        return next();
    }
}
