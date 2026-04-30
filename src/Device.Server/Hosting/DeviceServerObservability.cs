using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#observabilidade-tecnica
internal static class DeviceServerObservability
{
    public const string ActivitySourceName = "MicaAudio.DeviceServer";
    public const string MeterName = "MicaAudio.DeviceServer";
    public const string Component = "device-server";

    public const string TraceIdKey = "traceId";
    public const string SpanIdKey = "spanId";
    public const string ComponentKey = "component";
    public const string MicaComponentKey = "mica.component";
    public const string OperationKey = "operation";
    public const string DeviceIdKey = "deviceId";
    public const string CommandIdKey = "commandId";
    public const string AppIdKey = "appId";
    public const string CommandTypeKey = "commandType";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>("mica.device.command.duration", unit: "ms");
    private static readonly Counter<long> CommandTimeoutCount = Meter.CreateCounter<long>("mica.device.command.timeout.count");
    private static readonly Counter<long> CommandFailureCount = Meter.CreateCounter<long>("mica.device.command.failure.count");

    public static void ConfigureLogging(ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClearProviders();
        builder.Configure(logging =>
        {
            logging.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId
                | ActivityTrackingOptions.SpanId
                | ActivityTrackingOptions.ParentId;
        });
        builder.AddSerilog(Log.Logger, dispose: false);
    }

    public static void ConfigureOpenTelemetry(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!TryCreateExporterOptions(out var exporter))
        {
            return;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(builder => ConfigureResource(builder))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(exporter.ApplyTo))
            .WithMetrics(metrics => metrics
                .AddMeter(MeterName)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(exporter.ApplyTo));
    }

    public static Activity? StartCommandActivity(string operation, string deviceId, string commandId, string commandType)
    {
        var activity = ActivitySource.StartActivity(operation, ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ComponentKey, Component);
        activity.SetTag(MicaComponentKey, Component);
        activity.SetTag(OperationKey, operation);
        activity.SetTag(DeviceIdKey, deviceId);
        activity.SetTag(CommandIdKey, commandId);
        activity.SetTag(CommandTypeKey, commandType);
        return activity;
    }

    public static IDisposable PushScope(Activity? activity, params (string Key, object? Value)[] properties)
    {
        var disposables = new List<IDisposable>();

        if (activity is not null)
        {
            disposables.Add(LogContext.PushProperty(TraceIdKey, activity.TraceId.ToString()));
            disposables.Add(LogContext.PushProperty(SpanIdKey, activity.SpanId.ToString()));
        }

        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            disposables.Add(LogContext.PushProperty(key, value));
        }

        return new CompositeDisposable(disposables);
    }

    public static void SetException(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
    }

    public static void RecordCommandDuration(TimeSpan duration, string deviceId, string commandType, bool success, string stage)
    {
        var tags = new TagList
        {
            { ComponentKey, Component },
            { MicaComponentKey, Component },
            { DeviceIdKey, deviceId },
            { CommandTypeKey, commandType },
            { "success", success },
            { "stage", stage },
        };

        CommandDuration.Record(duration.TotalMilliseconds, tags);
    }

    public static void RecordCommandTimeout(string deviceId, string commandType)
    {
        var tags = new TagList
        {
            { ComponentKey, Component },
            { MicaComponentKey, Component },
            { DeviceIdKey, deviceId },
            { CommandTypeKey, commandType },
        };

        CommandTimeoutCount.Add(1, tags);
    }

    public static void RecordCommandFailure(string deviceId, string commandType, string stage)
    {
        var tags = new TagList
        {
            { ComponentKey, Component },
            { MicaComponentKey, Component },
            { DeviceIdKey, deviceId },
            { CommandTypeKey, commandType },
            { "stage", stage },
        };

        CommandFailureCount.Add(1, tags);
    }

    public static void LogHostMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Log.ForContext(ComponentKey, Component)
            .ForContext(MicaComponentKey, Component)
            .Information("{HostMessage}", message);
    }

    private static void ConfigureResource(ResourceBuilder builder)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var serviceVersion =
            entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? entryAssembly?.GetName().Version?.ToString()
            ?? "unknown";
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            serviceName = "mica-audio-desktop";
        }

        builder
            .AddService(
                serviceName: serviceName.Trim(),
                serviceVersion: serviceVersion,
                serviceInstanceId: $"{Environment.MachineName}-{Environment.ProcessId}")
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", "desktop-local"),
            ]);
    }

    private static bool TryCreateExporterOptions(out ExporterOptions exporter)
    {
        var endpointText = NormalizeOptional(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
        {
            exporter = default;
            return false;
        }

        exporter = new ExporterOptions(
            endpoint,
            NormalizeOptional(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS")),
            ParseProtocol(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")));
        return true;
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

    internal readonly record struct ExporterOptions(Uri Endpoint, string? Headers, OtlpExportProtocol Protocol)
    {
        public void ApplyTo(OtlpExporterOptions options)
        {
            options.Endpoint = Endpoint;
            options.Protocol = Protocol;

            if (!string.IsNullOrWhiteSpace(Headers))
            {
                options.Headers = Headers;
            }
        }
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> disposables;

        public CompositeDisposable(IReadOnlyList<IDisposable> disposables)
        {
            this.disposables = disposables;
        }

        public void Dispose()
        {
            for (var index = disposables.Count - 1; index >= 0; index--)
            {
                disposables[index].Dispose();
            }
        }
    }
}
