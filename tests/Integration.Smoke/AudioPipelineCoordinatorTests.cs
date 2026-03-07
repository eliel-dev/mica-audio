using System.Diagnostics;
using System.Threading.Channels;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;
using Visual.Win2D.Engine;

namespace Integration.Smoke;

public sealed class AudioPipelineCoordinatorTests
{
    [Fact]
    public async Task StartAsync_ShouldStartCaptureOnceAndDispatchSpectrumPayloads()
    {
        await using var capture = new FakeLoopbackCapture();
        var simulator = new RecordingLedOutput();
        var matrix = new RecordingLedOutput();
        var sink = new RecordingLedOutput();
        var analyzer = new FakeAnalyzer(CreateSpectrumFrame(0.4f));
        var coordinator = new AudioPipelineCoordinator(capture, simulator, matrix, sink, analyzer);

        await coordinator.StartAsync(hubPreviewEnabled: true, brightness: 0.6f, presetId: "preset");
        await capture.PublishAsync(new PcmFrame([1f, 2f, 3f], 42));
        await WaitForAsync(() => matrix.Payloads.Count == 1);

        Assert.Equal(1, capture.StartCalls);
        Assert.NotNull(capture.StartConfig);
        Assert.Equal(48_000, capture.StartConfig!.TargetSampleRate);
        Assert.Equal(1, capture.StartConfig.TargetChannels);
        Assert.Equal(8, capture.StartConfig.ChannelCapacity);
        Assert.Equal(12, capture.StartConfig.BufferMilliseconds);
        Assert.Single(matrix.Payloads);
        Assert.Single(simulator.Payloads);
        Assert.Empty(sink.Payloads);
        Assert.Equal(LedDefaults.MatrixWidth, matrix.Payloads[0].Bins128!.Length);

        await coordinator.StopAsync();
    }

    [Fact]
    public async Task StartAndStop_ShouldBeIdempotent()
    {
        await using var capture = new FakeLoopbackCapture();
        var analyzer = new FakeAnalyzer(CreateSpectrumFrame(0.2f));
        var coordinator = new AudioPipelineCoordinator(capture, new RecordingLedOutput(), new RecordingLedOutput(), new RecordingLedOutput(), analyzer);

        await coordinator.StartAsync(hubPreviewEnabled: false, brightness: 0.4f, presetId: "preset");
        await coordinator.StartAsync(hubPreviewEnabled: false, brightness: 0.8f, presetId: "preset");
        await coordinator.StopAsync();
        await coordinator.StopAsync();

        Assert.Equal(1, capture.StartCalls);
        Assert.Equal(1, capture.StopCalls);
    }

    [Fact]
    public async Task SetAnalyzer_ShouldUseUpdatedAnalyzerForSubsequentFrames()
    {
        await using var capture = new FakeLoopbackCapture();
        var matrix = new RecordingLedOutput();
        var coordinator = new AudioPipelineCoordinator(
            capture,
            new RecordingLedOutput(),
            matrix,
            new RecordingLedOutput(),
            new FakeAnalyzer(CreateSpectrumFrame(0.1f)));

        await coordinator.StartAsync(hubPreviewEnabled: false, brightness: 1f, presetId: "preset");
        await capture.PublishAsync(new PcmFrame([1f], 1));
        await WaitForAsync(() => matrix.Payloads.Count == 1);

        coordinator.SetAnalyzer(new FakeAnalyzer(CreateSpectrumFrame(0.9f)));
        await capture.PublishAsync(new PcmFrame([2f], 2));
        await WaitForAsync(() => matrix.Payloads.Count == 2);

        Assert.Equal(0.1f, matrix.Payloads[0].Level);
        Assert.Equal(0.9f, matrix.Payloads[1].Level);

        await coordinator.StopAsync();
    }

    [Fact]
    public async Task SetHubTransportMode_Frame128x64_ShouldSkipSpectrumDispatch()
    {
        await using var capture = new FakeLoopbackCapture();
        var matrix = new RecordingLedOutput();
        var coordinator = new AudioPipelineCoordinator(
            capture,
            new RecordingLedOutput(),
            matrix,
            new RecordingLedOutput(),
            new FakeAnalyzer(CreateSpectrumFrame(0.5f)));

        coordinator.SetHubTransportMode(RendererHubTransportMode.Frame128x64);
        await coordinator.StartAsync(hubPreviewEnabled: false, brightness: 1f, presetId: "preset");
        await capture.PublishAsync(new PcmFrame([1f], 1));
        await Task.Delay(50);

        Assert.NotNull(coordinator.LatestFrame);
        Assert.Empty(matrix.Payloads);

        await coordinator.StopAsync();
    }

    [Fact]
    public async Task SendHubFrame_ShouldForceSimulatorWhenPreviewIsDisabled()
    {
        await using var capture = new FakeLoopbackCapture();
        var simulator = new RecordingLedOutput();
        var matrix = new RecordingLedOutput();
        var sink = new RecordingLedOutput();
        var coordinator = new AudioPipelineCoordinator(capture, simulator, matrix, sink, new FakeAnalyzer(CreateSpectrumFrame(0.5f)));

        coordinator.ConfigureHubOutputs(enableSimulator: false, brightness: 0.5f);
        coordinator.SendHubFrame(Enumerable.Repeat(new RgbaColor(1, 2, 3, 4), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray(), forceSimulator: true);

        Assert.Single(matrix.Payloads);
        Assert.Single(simulator.Payloads);
        Assert.Empty(sink.Payloads);
    }

    private static SpectrumFrame CreateSpectrumFrame(float level)
    {
        var bands = Enumerable.Repeat(level, 64).ToArray();
        return new SpectrumFrame(bands, bands, level, Stopwatch.GetTimestamp());
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not satisfied in time.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class FakeAnalyzer : IAnalyzer
    {
        private readonly SpectrumFrame frame;

        public FakeAnalyzer(SpectrumFrame frame)
        {
            this.frame = frame;
        }

        public SpectrumFrame? Process(in PcmFrame frame)
        {
            return this.frame;
        }
    }

    private sealed class RecordingLedOutput : ILedOutput
    {
        public bool IsAvailable => true;

        public List<LedPayload> Payloads { get; } = [];

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public void Start(LedOutputConfig config)
        {
            StartCalls++;
        }

        public void Stop()
        {
            StopCalls++;
        }

        public void Send(LedPayload payload)
        {
            Payloads.Add(payload);
        }

        public void SetBrightness(float value)
        {
        }
    }

    private sealed class FakeLoopbackCapture : ILoopbackCapture
    {
        private readonly Channel<PcmFrame> channel = Channel.CreateUnbounded<PcmFrame>();

        public ChannelReader<PcmFrame> Frames => channel.Reader;

        public event EventHandler<CaptureStatusChangedEventArgs>? StatusChanged;

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public CaptureConfig? StartConfig { get; private set; }

        public Task StartAsync(CaptureConfig config, CancellationToken cancellationToken = default)
        {
            StartCalls++;
            StartConfig = config;
            StatusChanged?.Invoke(this, new CaptureStatusChangedEventArgs(CaptureStatus.Running, "started"));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalls++;
            channel.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public async Task PublishAsync(PcmFrame frame)
        {
            await channel.Writer.WriteAsync(frame);
        }

        public ValueTask DisposeAsync()
        {
            channel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
