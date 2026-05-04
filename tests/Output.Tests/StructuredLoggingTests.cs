using App.WinUI.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Output.Tests;

public sealed class StructuredLoggingTests
{
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> LogStructuredTest =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(9000, nameof(StructuredLoggingTests)),
            "structured test");

    [Fact]
    public void BeginScope_ShouldPopulateStructuredProperties_ForSerilogProvider()
    {
        using var activities = new ActivityCapture(AppObservability.ActivitySourceName);
        var sink = new CollectingSink();
        using var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(serilogLogger, dispose: false));
        var logger = loggerFactory.CreateLogger("observability-test");

        using var activity = AppObservability.StartActivity("device.command", AppObservability.DeviceIntegrationComponent);
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.DeviceIdKey, "device-1"),
            (AppObservability.CommandIdKey, "cmd-1"),
            (AppObservability.AppIdKey, "app-1"));

        LogStructuredTest(logger, null);

        var evt = Assert.Single(sink.Events);
        Assert.Equal("device-1", AssertScalar(evt, AppObservability.DeviceIdKey));
        Assert.Equal("cmd-1", AssertScalar(evt, AppObservability.CommandIdKey));
        Assert.Equal("app-1", AssertScalar(evt, AppObservability.AppIdKey));
        Assert.False(string.IsNullOrWhiteSpace(AssertScalar(evt, AppObservability.TraceIdKey)));
        Assert.False(string.IsNullOrWhiteSpace(AssertScalar(evt, AppObservability.SpanIdKey)));
    }

    private static string AssertScalar(LogEvent evt, string propertyName)
    {
        var scalar = Assert.IsType<ScalarValue>(evt.Properties[propertyName]);
        return Assert.IsType<string>(scalar.Value);
    }
}
