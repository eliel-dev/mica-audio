using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Serilog.Core;
using Serilog.Events;

namespace Output.Tests;

internal sealed class ActivityCapture : IDisposable
{
    private readonly ConcurrentQueue<Activity> activities = new();
    private readonly ActivityListener listener;

    public ActivityCapture(string sourceName)
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, sourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Enqueue(activity),
        };

        ActivitySource.AddActivityListener(listener);
    }

    public IReadOnlyList<Activity> CompletedActivities => activities.ToArray();

    public void Dispose()
    {
        listener.Dispose();
    }
}

internal sealed class MetricCapture : IDisposable
{
    private readonly ConcurrentQueue<MetricMeasurement> measurements = new();
    private readonly MeterListener listener = new();
    private readonly string meterName;

    public MetricCapture(string meterName)
    {
        this.meterName = meterName;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (string.Equals(instrument.Meter.Name, this.meterName, StringComparison.Ordinal))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(OnDoubleMeasurement);
        listener.SetMeasurementEventCallback<long>(OnLongMeasurement);
        listener.Start();
    }

    public IReadOnlyList<MetricMeasurement> Measurements => measurements.ToArray();

    public void Dispose()
    {
        listener.Dispose();
    }

    private void OnDoubleMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        measurements.Enqueue(new MetricMeasurement(instrument.Name, measurement, ToDictionary(tags)));
    }

    private void OnLongMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        measurements.Enqueue(new MetricMeasurement(instrument.Name, measurement, ToDictionary(tags)));
    }

    private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            result[tag.Key] = tag.Value;
        }

        return result;
    }
}

internal sealed record MetricMeasurement(string InstrumentName, object Value, IReadOnlyDictionary<string, object?> Tags);

internal sealed class CollectingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> events = new();

    public IReadOnlyList<LogEvent> Events => events.ToArray();

    public void Emit(LogEvent logEvent)
    {
        events.Enqueue(logEvent);
    }
}

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly IReadOnlyDictionary<string, string?> previousValues;

    public EnvironmentVariableScope(params (string Name, string? Value)[] variables)
    {
        previousValues = variables.ToDictionary(
            static item => item.Name,
            static item => Environment.GetEnvironmentVariable(item.Name),
            StringComparer.Ordinal);

        foreach (var (name, value) in variables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in previousValues)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
