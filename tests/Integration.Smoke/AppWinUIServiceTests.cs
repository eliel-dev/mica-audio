using System.Threading.Channels;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Integration.Smoke;

public sealed class AppWinUIServiceTests
{
    [Fact]
    public void SettingsDomainService_MigratesLegacyAndCopies()
    {
        var service = new AppSettingsDomainService();
        var migrated = service.Migrate(new AppSettings
        {
            ActivePresetId = "",
            Sensitivity = -10,
            SensitivityMinDb = -85,
            SensitivityMaxDb = -25,
            FftSize = 123,
            FftSmoothing = 9,
            FrequencyMinHz = 5000,
            FrequencyMaxHz = 100,
        });

        Assert.Equal("audiomotion-clone", migrated.ActivePresetId);
        Assert.True(migrated.SensitivityMaxDb > migrated.SensitivityMinDb);

        var copied = service.Copy(migrated, b =>
        {
            b.SetBarCount(72);
            b.SetFrequencyRange(40, 16000);
        });

        Assert.Equal(72, copied.BarCount);
        Assert.Equal(40, copied.FrequencyMinHz);
        Assert.Equal(16000, copied.FrequencyMaxHz);
    }

    [Fact]
    public async Task AudioPipelineCoordinator_Smoke_StartReadStop()
    {
        var capture = new FakeCapture();
        var analyzer = new FakeAnalyzer();
        var led = new FakeLedOutput();
        var coordinator = new AudioPipelineCoordinator(capture, led, new FakeLedOutput(), () => analyzer);

        await coordinator.StartAsync(hubPreviewEnabled: true, brightness: 0.8f, presetId: "preset-x");
        await capture.Writer.WriteAsync(new PcmFrame(new float[] { 1f, 2f }, 1));
        await Task.Delay(20);
        await coordinator.StopAsync();

        Assert.NotNull(coordinator.LatestFrame);
        Assert.True(led.SendCount >= 1);
    }


    [Fact]
    public void MainPage_Smoke_TypeIsLoadable()
    {
        var type = typeof(App.WinUI.Views.MainPage);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
    }
    private sealed class FakeAnalyzer : IAnalyzer
    {
        public SpectrumFrame? Process(in PcmFrame frame) => new([0.1f], [0.2f], 0.3f, frame.TimestampQpc);
    }

    private sealed class FakeLedOutput : ILedOutput
    {
        public int SendCount { get; private set; }
        public bool IsAvailable => true;
        public void Start(MicaAudio.Core.Led.LedOutputConfig config) { }
        public void Stop() { }
        public void Send(MicaAudio.Core.Led.LedPayload payload) => SendCount++;
        public void SetBrightness(float value) { }
    }

    private sealed class FakeCapture : ILoopbackCapture
    {
        private readonly Channel<PcmFrame> channel = Channel.CreateUnbounded<PcmFrame>();
        public ChannelWriter<PcmFrame> Writer => channel.Writer;
        public ChannelReader<PcmFrame> Frames => channel.Reader;
        public event EventHandler<CaptureStatusChangedEventArgs>? StatusChanged;
        public Task StartAsync(CaptureConfig config, CancellationToken cancellationToken = default)
        {
            StatusChanged?.Invoke(this, new CaptureStatusChangedEventArgs(CaptureStatus.Running, "running"));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            channel.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
