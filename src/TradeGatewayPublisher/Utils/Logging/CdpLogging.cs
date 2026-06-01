using System.Diagnostics.CodeAnalysis;
using Defra.TradeImports.Tracing;
using Elastic.Serilog.Enrichers.Web;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Enrichers;
using Serilog.Events;
using TradeGatewayPublisher.Utils.Auditing;

namespace TradeGatewayPublisher.Utils.Logging;

public static class CdpLogging
{
    [ExcludeFromCodeCoverage]
    public static void Configuration(HostBuilderContext ctx, LoggerConfiguration config)
    {
        var httpAccessor = ctx.Configuration.Get<HttpContextAccessor>();
        var traceIdHeader = ctx.Configuration.GetValue<string>("TraceHeader");

        var mainLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.WithEcsHttpContext(httpAccessor!)
            .Enrich.FromLogContext()
            .Filter.With<AuditLogger.Filters.ExcludeAuditEvents>()
            .CreateLogger();

        if (traceIdHeader != null)
        {
            config.Enrich.WithCorrelationId(traceIdHeader);
            config.Enrich.WithTraceId(traceIdHeader);
        }

        var auditLogger = AuditLogger.CreateAuditLogger();

        config.WriteTo.Logger(mainLogger).WriteTo.Logger(auditLogger);
    }
}

public class TraceIdEnricher : ILogEventEnricher
{
    private const string PropertyName = "CorrelationId";
    private readonly bool _addValueIfHeaderAbsence;
    private readonly ITraceContextAccessor _contextAccessor;

    internal TraceIdEnricher(string headerKey, bool addValueIfHeaderAbsence, ITraceContextAccessor contextAccessor)
    {
        _addValueIfHeaderAbsence = addValueIfHeaderAbsence;
        _contextAccessor = contextAccessor;
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = _contextAccessor.Context;
        if (context == null)
            return;

        var correlationId = string.Empty;

        if (!string.IsNullOrWhiteSpace(context.TraceId))
            correlationId = context.TraceId;
        else if (_addValueIfHeaderAbsence)
            correlationId = Guid.NewGuid().ToString("N");

        LogEventProperty correlationIdProperty = new(PropertyName, new ScalarValue(correlationId));
        logEvent.AddOrUpdateProperty(correlationIdProperty);
    }
}

public static class ClientInfoLoggerConfigurationExtensions
{
    public static LoggerConfiguration WithTraceId(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        string headerName = "x-correlation-id",
        bool addValueIfHeaderAbsence = true
    )
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);

        return enrichmentConfiguration.With(
            new TraceIdEnricher(headerName, addValueIfHeaderAbsence, new TraceContextAccessor())
        );
    }
}
