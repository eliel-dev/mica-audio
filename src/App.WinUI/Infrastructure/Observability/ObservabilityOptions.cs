using System.Reflection;
using MicaAudio.Core.Config;
using OpenTelemetry.Exporter;

namespace App.WinUI.Infrastructure.Observability;

// DOCS: docs/wiki/modules/app-winui.md#observabilidade-tecnica
internal sealed class ObservabilityOptions
{
    private const string DefaultServiceName = "mica-audio-desktop";

    public required string ServiceName { get; init; }

    public required string ServiceVersion { get; init; }

    public required string ServiceInstanceId { get; init; }

    public required string EngineeringLogPath { get; init; }

    public Uri? OtlpEndpoint { get; init; }

    public string? OtlpHeaders { get; init; }

    public OtlpExportProtocol OtlpProtocol { get; init; } = OtlpExportProtocol.HttpProtobuf;

    public bool HasOtlpEndpoint => OtlpEndpoint is not null;

    public static ObservabilityOptions Load(MicaAudioOptions appOptions)
    {
        ArgumentNullException.ThrowIfNull(appOptions);

        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            serviceName = DefaultServiceName;
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        var serviceVersion =
            entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? entryAssembly?.GetName().Version?.ToString()
            ?? "unknown";

        var localRoot = Path.GetDirectoryName(appOptions.CrashLogPath);
        if (string.IsNullOrWhiteSpace(localRoot))
        {
            localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicaAudio");
        }

        var endpointText = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        return new ObservabilityOptions
        {
            ServiceName = serviceName.Trim(),
            ServiceVersion = serviceVersion,
            ServiceInstanceId = $"{Environment.MachineName}-{Environment.ProcessId}",
            EngineeringLogPath = Path.Combine(localRoot, "logs", "engineering-.clef"),
            OtlpEndpoint = TryParseEndpoint(endpointText),
            OtlpHeaders = NormalizeOptional(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS")),
            OtlpProtocol = ParseProtocol(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")),
        };
    }

    internal void ApplyTo(OtlpExporterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (OtlpEndpoint is not null)
        {
            options.Endpoint = OtlpEndpoint;
        }

        options.Protocol = OtlpProtocol;

        if (!string.IsNullOrWhiteSpace(OtlpHeaders))
        {
            options.Headers = OtlpHeaders;
        }
    }

    private static Uri? TryParseEndpoint(string? value)
    {
        var normalized = NormalizeOptional(value);
        return Uri.TryCreate(normalized, UriKind.Absolute, out var endpoint)
            ? endpoint
            : null;
    }

    private static OtlpExportProtocol ParseProtocol(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return OtlpExportProtocol.HttpProtobuf;
        }

        return normalized.ToLowerInvariant() switch
        {
            "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            "httpprotobuf" => OtlpExportProtocol.HttpProtobuf,
            "http-protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => OtlpExportProtocol.HttpProtobuf,
        };
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
