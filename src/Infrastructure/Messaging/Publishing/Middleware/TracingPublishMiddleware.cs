using Defra.TradeImports.Tracing;

namespace Infrastructure.Messaging.Publishing.Middleware;

public class TracingPublishMiddleware(ITraceContextAccessor traceContextAccessor) : IPublishMiddleware
{
    public Task InvokeAsync(IPublishContext context, Func<Task> next, CancellationToken cancellationToken = default)
    {
        context.SetTraceId(traceContextAccessor.Context?.TraceId);
        return next();
    }
}
