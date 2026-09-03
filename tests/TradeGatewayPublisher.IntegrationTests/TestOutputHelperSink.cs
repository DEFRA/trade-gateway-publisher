using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace TradeGatewayPublisher.IntegrationTests;

/// <summary>
/// A single app host (and therefore a single Serilog logger) is shared across every test in the
/// collection - see <see cref="IntegrationTestFixture" />. This sink routes each log event to
/// whichever test is currently running via a static "current" helper, rather than baking a fixed
/// <see cref="ITestOutputHelper" /> into the logger at host build time. The collection disables
/// parallelization, so exactly one test owns this at a time.
/// </summary>
public sealed class TestOutputHelperSink(IFormatProvider? formatProvider = null) : ILogEventSink
{
    private static ITestOutputHelper? s_current;

    public static IDisposable Capture(ITestOutputHelper testOutputHelper)
    {
        s_current = testOutputHelper;
        return new Releaser();
    }

    public void Emit(LogEvent logEvent)
    {
        var testOutputHelper = s_current;
        if (testOutputHelper is null)
            return;

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

    private sealed class Releaser : IDisposable
    {
        public void Dispose() => s_current = null;
    }
}

public static class TestOutputHelperSinkExtensions
{
    public static Serilog.LoggerConfiguration TestOutputHelper(
        this LoggerSinkConfiguration sinkConfiguration,
        IFormatProvider? formatProvider = null
    )
    {
        return sinkConfiguration.Sink(new TestOutputHelperSink(formatProvider));
    }
}
