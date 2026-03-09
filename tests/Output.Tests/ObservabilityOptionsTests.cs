using App.WinUI.Infrastructure.Observability;
using MicaAudio.Core.Config;
using OpenTelemetry.Exporter;

namespace Output.Tests;

public sealed class ObservabilityOptionsTests
{
    [Fact]
    public void Load_ShouldDisableOtlp_WhenEndpointIsMissing()
    {
        using var env = new EnvironmentVariableScope(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", null),
            ("OTEL_EXPORTER_OTLP_HEADERS", null),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", null),
            ("OTEL_SERVICE_NAME", null));

        var options = ObservabilityOptions.Load(CreateAppOptions());

        Assert.False(options.HasOtlpEndpoint);
        Assert.Null(options.OtlpEndpoint);
        Assert.Equal("mica-audio-desktop", options.ServiceName);
        Assert.EndsWith(Path.Combine("logs", "engineering-.clef"), options.EngineeringLogPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_ShouldReadStandardOtelEnvironmentVariables()
    {
        using var env = new EnvironmentVariableScope(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "https://otel.example:4318"),
            ("OTEL_EXPORTER_OTLP_HEADERS", "Authorization=Bearer 123,tenant=test"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc"),
            ("OTEL_SERVICE_NAME", "mica-audio-custom"));

        var options = ObservabilityOptions.Load(CreateAppOptions());
        var exporter = new OtlpExporterOptions();
        options.ApplyTo(exporter);

        Assert.True(options.HasOtlpEndpoint);
        Assert.Equal(new Uri("https://otel.example:4318"), options.OtlpEndpoint);
        Assert.Equal("Authorization=Bearer 123,tenant=test", options.OtlpHeaders);
        Assert.Equal(OtlpExportProtocol.Grpc, options.OtlpProtocol);
        Assert.Equal("mica-audio-custom", options.ServiceName);
        Assert.Equal(new Uri("https://otel.example:4318"), exporter.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, exporter.Protocol);
        Assert.Equal("Authorization=Bearer 123,tenant=test", exporter.Headers);
    }

    private static MicaAudioOptions CreateAppOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        return new MicaAudioOptions
        {
            CrashLogPath = Path.Combine(root, "crash.log"),
        };
    }
}
