using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace TradeGatewayPublisher.IntegrationTests;

public sealed class TestOutputHelperSink(ITestOutputHelper testOutputHelper, IFormatProvider? formatProvider = null)
    : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        try
        {
            testOutputHelper.WriteLine(
                $"{logEvent.Timestamp:HH:mm:ss.fff} [{logEvent.Level}] {logEvent.RenderMessage(formatProvider)}"
            );

            if (logEvent.Exception != null)
                testOutputHelper.WriteLine(logEvent.Exception.ToString());
        }
        catch (InvalidOperationException)
        {
            // The test may have already completed (e.g. a background job logging after the
            // test finishes) - ITestOutputHelper throws once its test is done, nothing to do here.
        }
    }
}

public static class TestOutputHelperSinkExtensions
{
    public static Serilog.LoggerConfiguration TestOutputHelper(
        this LoggerSinkConfiguration sinkConfiguration,
        ITestOutputHelper testOutputHelper,
        IFormatProvider? formatProvider = null
    )
    {
        return sinkConfiguration.Sink(new TestOutputHelperSink(testOutputHelper, formatProvider));
    }
}
