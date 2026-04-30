using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using MicaAudio.Core.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace App.WinUI.Infrastructure.Observability;

// DOCS: docs/wiki/modules/app-winui.md#observabilidade-tecnica
internal static class AppObservability
{
    public const string ActivitySourceName = "MicaAudio.App";
    public const string MeterName = "MicaAudio.App";
    public const string AppComponent = "app-winui";
    public const string DeviceIntegrationComponent = "device-integration";

    public const string TraceIdKey = "traceId";
    public const string SpanIdKey = "spanId";
    public const string ComponentKey = "component";
    public const string MicaComponentKey = "mica.component";
    public const string OperationKey = "operation";
    public const string FeatureKey = "feature";
    public const string DeviceIdKey = "deviceId";
    public const string CommandIdKey = "commandId";
    public const string AppIdKey = "appId";
    public const string PortNameKey = "portName";
    public const string HttpClientNameKey = "http.client.name";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> DeployDuration = Meter.CreateHistogram<double>("mica.app.deploy.duration", unit: "ms");
    private static readonly Histogram<double> OnboardingFlashDuration = Meter.CreateHistogram<double>("mica.onboarding.flash.duration", unit: "ms");
    private static readonly Counter<long> UiErrorCount = Meter.CreateCounter<long>("mica.ui.error.count");

    public static ObservabilityOptions ConfigureGlobalLogger(MicaAudioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var observability = ObservabilityOptions.Load(options);
        Directory.CreateDirectory(Path.GetDirectoryName(observability.EngineeringLogPath)!);

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service.name", observability.ServiceName)
            .Enrich.WithProperty("service.version", observability.ServiceVersion)
            .Enrich.WithProperty("service.instance.id", observability.ServiceInstanceId)
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: observability.EngineeringLogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true);

#if DEBUG
        loggerConfiguration = loggerConfiguration.WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture);
#endif

        if (observability.HasOtlpEndpoint)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.OpenTelemetry(exporterOptions =>
            {
                exporterOptions.Endpoint = observability.OtlpEndpoint!.ToString();
                exporterOptions.Protocol = observability.OtlpProtocol switch
                {
                    OpenTelemetry.Exporter.OtlpExportProtocol.Grpc => OtlpProtocol.Grpc,
                    _ => OtlpProtocol.HttpProtobuf,
                };
                var headers = ParseOtlpHeaders(observability.OtlpHeaders);
                if (headers is not null)
                {
                    exporterOptions.Headers = headers;
                }

                exporterOptions.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = observability.ServiceName,
                    ["service.version"] = observability.ServiceVersion,
                    ["service.instance.id"] = observability.ServiceInstanceId,
                };
                exporterOptions.IncludedData =
                    IncludedData.MessageTemplateTextAttribute
                    | IncludedData.TraceIdField
                    | IncludedData.SpanIdField
                    | IncludedData.SpecRequiredResourceAttributes;
                exporterOptions.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
            });
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        return observability;
    }

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

    public static void ConfigureOpenTelemetry(IServiceCollection services, ObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.HasOtlpEndpoint)
        {
            return;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(builder => ConfigureResource(builder, options))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options.ApplyTo))
            .WithMetrics(metrics => metrics
                .AddMeter(MeterName)
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options.ApplyTo));
    }

    public static Activity? StartActivity(string operationName, string component, ActivityKind kind = ActivityKind.Internal)
    {
        var activity = ActivitySource.StartActivity(operationName, kind);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ComponentKey, component);
        activity.SetTag(MicaComponentKey, component);
        activity.SetTag(OperationKey, operationName);
        return activity;
    }

    public static IDisposable? BeginScope(Microsoft.Extensions.Logging.ILogger? logger, Activity? activity, params (string Key, object? Value)[] properties)
    {
        if (logger is null)
        {
            return null;
        }

        var scope = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (activity is not null)
        {
            scope[TraceIdKey] = activity.TraceId.ToString();
            scope[SpanIdKey] = activity.SpanId.ToString();
        }

        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            scope[key] = value;
        }

        return scope.Count == 0 ? null : logger.BeginScope(scope);
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

    public static void RecordDeployDuration(TimeSpan duration, string? deviceId, string? appId, bool success)
    {
        var tags = new TagList
        {
            { ComponentKey, AppComponent },
            { MicaComponentKey, AppComponent },
            { DeviceIdKey, deviceId },
            { AppIdKey, appId },
            { "success", success },
        };

        DeployDuration.Record(duration.TotalMilliseconds, tags);
    }

    public static void RecordOnboardingFlashDuration(TimeSpan duration, string? portName, bool success)
    {
        var tags = new TagList
        {
            { ComponentKey, AppComponent },
            { MicaComponentKey, AppComponent },
            { PortNameKey, portName },
            { "success", success },
        };

        OnboardingFlashDuration.Record(duration.TotalMilliseconds, tags);
    }

    public static void RecordUiError(string operation, string? component = null)
    {
        var normalizedComponent = string.IsNullOrWhiteSpace(component) ? AppComponent : component;
        var tags = new TagList
        {
            { ComponentKey, normalizedComponent },
            { MicaComponentKey, normalizedComponent },
            { OperationKey, operation },
        };

        UiErrorCount.Add(1, tags);
    }

    private static void ConfigureResource(ResourceBuilder builder, ObservabilityOptions options)
    {
        builder
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: options.ServiceInstanceId)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", "desktop-local"),
            ]);
    }

    private static Dictionary<string, string>? ParseOtlpHeaders(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var headerValue = segment[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            result[key] = headerValue;
        }

        return result.Count == 0 ? null : result;
    }
}
