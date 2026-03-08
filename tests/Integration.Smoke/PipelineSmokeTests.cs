using Analyzer.Dsp.Analysis;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using Output.Led;

namespace Integration.Smoke;

public class PipelineSmokeTests
{
    [Fact]
    public void AnalyzerToOutput_ShouldPass128Bands()
    {
        var analyzer = new SpectrumAnalyzer(new AnalyzerConfig());
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig { Width = LedDefaults.MatrixWidth, Height = LedDefaults.MatrixHeight, Brightness = 0.8f });

        var samples = new float[4096];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = MathF.Sin(2f * MathF.PI * 220f * i / 48_000f);
        }

        var frame = analyzer.Process(new PcmFrame(samples, 456));
        Assert.NotNull(frame);

        simulator.Send(LedPayloadFactory.CreateSpectrumPayload(frame!, "smoke"));

        var snapshot = simulator.GetFrameSnapshot();
        Assert.Equal(128 * 64, snapshot.Length);
    }

    [Fact(Skip = "Manual validation for real WASAPI loopback session.")]
    public async Task Loopback_ShouldRunForManualValidation()
    {
        await using var capture = new WasapiLoopbackCaptureService();
        await capture.StartAsync(new CaptureConfig());
        await Task.Delay(TimeSpan.FromSeconds(5));
        await capture.StopAsync();
    }
}
