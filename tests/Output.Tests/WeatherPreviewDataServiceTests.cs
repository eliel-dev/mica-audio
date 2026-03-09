using System.Diagnostics;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Logging;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using Polly.Timeout;

namespace Output.Tests;

public sealed class WeatherPreviewDataServiceTests
{
    [Fact]
    public async Task GetSnapshot_ShouldStartLoadingWithFixedTimboLocation_WhenRefreshBegins()
    {
        var client = new FakeWeatherForecastClient();
        var service = new WeatherPreviewDataService(client, appLogStore: null, nowProvider: null, refreshInterval: null, failureRetryInterval: null);

        var snapshot = service.GetSnapshot();

        Assert.Equal(WeatherAppFixedLocation.FixedCityDisplayName, snapshot.CityDisplay);
        Assert.Equal(WeatherPreviewLoadState.Loading, snapshot.State);
        Assert.Equal("C", snapshot.UnitSymbol);

        await client.WaitForInvocationAsync(0);
    }

    [Fact]
    public async Task GetSnapshot_ShouldReturnLiveData_WhenClientSucceeds()
    {
        var client = new FakeWeatherForecastClient();
        var service = new WeatherPreviewDataService(client, appLogStore: null, nowProvider: null, refreshInterval: null, failureRetryInterval: null);

        _ = service.GetSnapshot();
        await client.WaitForInvocationAsync(0);
        client.CompleteSuccess(0, temperature: 19.9, weatherCode: 3);

        var snapshot = await WaitForSnapshotAsync(service, static s => s.State == WeatherPreviewLoadState.Live);

        Assert.Equal(WeatherAppFixedLocation.FixedCityDisplayName, snapshot.CityDisplay);
        Assert.NotNull(snapshot.Temperature);
        Assert.Equal(19.9, snapshot.Temperature.Value, 1);
        Assert.Equal(3, snapshot.WeatherCode);
        Assert.Equal("C", snapshot.UnitSymbol);
        Assert.True(snapshot.HasLiveData);
    }

    [Fact]
    public async Task GetSnapshot_ShouldReturnErrorState_AndRecordLog_WhenClientFails()
    {
        var root = CreateTempRoot();
        try
        {
            var currentTime = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
            var client = new FakeWeatherForecastClient();
            var logStore = CreateLogStore(root, () => currentTime);
            var service = new WeatherPreviewDataService(client, logStore, logger: null, nowProvider: () => currentTime);

            _ = service.GetSnapshot();
            await client.WaitForInvocationAsync(0);
            client.CompleteFailure(0, new HttpRequestException("network down"));

            var snapshot = await WaitForSnapshotAsync(service, static s => s.State == WeatherPreviewLoadState.Error);
            var entries = logStore.GetByAppId(WeatherAppFixedLocation.AppId);

            Assert.Equal(WeatherAppFixedLocation.FixedCityDisplayName, snapshot.CityDisplay);
            Assert.Equal(WeatherPreviewLoadState.Error, snapshot.State);
            Assert.NotNull(snapshot.FailureMessage);
            Assert.Single(entries);
            Assert.Contains("Preview do clima indisponivel", entries[0].Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetSnapshot_ShouldRecoverAfterFailure_WithoutDuplicatingLogInsideRetryWindow()
    {
        var root = CreateTempRoot();
        try
        {
            var currentTime = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
            var client = new FakeWeatherForecastClient();
            var logStore = CreateLogStore(root, () => currentTime);
            var service = new WeatherPreviewDataService(client, logStore, logger: null, nowProvider: () => currentTime);

            _ = service.GetSnapshot();
            await client.WaitForInvocationAsync(0);
            client.CompleteFailure(0, new HttpRequestException("network down"));

            _ = await WaitForSnapshotAsync(service, static s => s.State == WeatherPreviewLoadState.Error);
            Assert.Single(logStore.GetByAppId(WeatherAppFixedLocation.AppId));

            currentTime = currentTime.AddSeconds(10);
            var retryWindowSnapshot = service.GetSnapshot();
            Assert.Equal(WeatherPreviewLoadState.Error, retryWindowSnapshot.State);
            Assert.Equal(1, client.InvocationCount);
            Assert.Single(logStore.GetByAppId(WeatherAppFixedLocation.AppId));

            currentTime = currentTime.AddSeconds(31);
            _ = service.GetSnapshot();
            await client.WaitForInvocationAsync(1);
            client.CompleteSuccess(1, temperature: 21.4, weatherCode: 1);

            var recovered = await WaitForSnapshotAsync(service, static s => s.State == WeatherPreviewLoadState.Live);

            Assert.NotNull(recovered.Temperature);
            Assert.Equal(21.4, recovered.Temperature.Value, 1);
            Assert.Equal(1, recovered.WeatherCode);
            Assert.Single(logStore.GetByAppId(WeatherAppFixedLocation.AppId));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetSnapshot_ShouldReportTimeoutMessage_WhenClientTimesOut()
    {
        var client = new FakeWeatherForecastClient();
        var service = new WeatherPreviewDataService(client, appLogStore: null, nowProvider: null, refreshInterval: null, failureRetryInterval: null);

        _ = service.GetSnapshot();
        await client.WaitForInvocationAsync(0);
        client.CompleteFailure(0, new TimeoutRejectedException("budget exhausted"));

        var snapshot = await WaitForSnapshotAsync(service, static s => s.State == WeatherPreviewLoadState.Error);

        Assert.Equal(WeatherPreviewLoadState.Error, snapshot.State);
        Assert.Contains("tempo limite", snapshot.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSnapshot_ShouldEmitRefreshActivity()
    {
        var client = new FakeWeatherForecastClient();
        using var capture = new ActivityCapture(AppObservability.ActivitySourceName);
        var service = new WeatherPreviewDataService(client, appLogStore: null, nowProvider: null, refreshInterval: null, failureRetryInterval: null);

        _ = service.GetSnapshot();
        await client.WaitForInvocationAsync(0);
        client.CompleteSuccess(0, temperature: 18.4, weatherCode: 1);
        _ = await WaitForSnapshotAsync(service, static s => s.State == WeatherPreviewLoadState.Live);

        var activity = Assert.Single(capture.CompletedActivities, item => item.OperationName == "weather-preview.refresh");
        Assert.Equal("weather-preview", activity.GetTagItem(AppObservability.FeatureKey));
        Assert.Equal(WeatherAppFixedLocation.AppId, activity.GetTagItem(AppObservability.AppIdKey));
        Assert.Equal(18.4, activity.GetTagItem("weather.temperature"));
    }

    private static async Task<WeatherPreviewSnapshot> WaitForSnapshotAsync(
        WeatherPreviewDataService service,
        Func<WeatherPreviewSnapshot, bool> predicate,
        int timeoutMs = 2000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var snapshot = service.GetSnapshot();
            if (predicate(snapshot))
            {
                return snapshot;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("O snapshot do clima nao atingiu o estado esperado.");
    }

    private static AppLogStore CreateLogStore(string root, Func<DateTimeOffset> nowProvider)
    {
        return new AppLogStore(
            Options.Create(new MicaAudioOptions
            {
                CrashLogPath = Path.Combine(root, "crash.log"),
            }),
            nowProvider);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-weather-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeWeatherForecastClient : WeatherPreviewDataService.IWeatherForecastClient
    {
        private readonly object gate = new();
        private readonly List<TaskCompletionSource<WeatherPreviewDataService.CurrentWeatherData>> responses = [];

        public int InvocationCount
        {
            get
            {
                lock (gate)
                {
                    return responses.Count;
                }
            }
        }

        public Task<WeatherPreviewDataService.CurrentWeatherData> GetCurrentAsync(float latitude, float longitude, string timezone)
        {
            var response = new TaskCompletionSource<WeatherPreviewDataService.CurrentWeatherData>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                responses.Add(response);
            }

            return response.Task;
        }

        public async Task WaitForInvocationAsync(int index, int timeoutMs = 2000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                lock (gate)
                {
                    if (responses.Count > index)
                    {
                        return;
                    }
                }

                await Task.Delay(20);
            }

            throw new TimeoutException($"A chamada {index} nao foi disparada.");
        }

        public void CompleteSuccess(int index, double temperature, int weatherCode)
        {
            GetResponse(index).TrySetResult(new WeatherPreviewDataService.CurrentWeatherData(temperature, weatherCode));
        }

        public void CompleteFailure(int index, Exception exception)
        {
            GetResponse(index).TrySetException(exception);
        }

        private TaskCompletionSource<WeatherPreviewDataService.CurrentWeatherData> GetResponse(int index)
        {
            lock (gate)
            {
                return responses[index];
            }
        }
    }
}
