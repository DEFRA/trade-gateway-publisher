using Defra.TradeImports.Tracing;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SharpCompress.Common.Ace.Headers;

namespace Infrastructure.Messaging.Consuming;

public class TracingConsumeMiddleware(
    ITraceContextAccessor traceContextAccessor,
    HeaderPropagationValues headerPropagationValues
) : IConsumeMiddleware
{
    public Task InvokeAsync(MessageContext context, Func<Task> next, CancellationToken cancellationToken = default)
    {
        // Setting the trace context will take either the trace ID from the incoming
        // message headers or it will start a new trace ID that may be propagated onwards
        // to any nested HTTP calls or further message publishing
        traceContextAccessor.Context = new TraceContext { TraceId = context.GetTraceId() };

        // As per the middleware implementation for header propagation, the following sets
        // the headerPropagationValues.Headers value so it can be used by any configured
        // HTTP handler
        var headers = headerPropagationValues.Headers ??= new Dictionary<string, StringValues>(
            StringComparer.OrdinalIgnoreCase
        );
        headers.Add(MetricNames.TraceKey, traceContextAccessor.Context.TraceId);

        return next();
    }
}
