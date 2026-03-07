using Audio.Loopback.Capture;
using NAudio.Wave;

namespace Integration.Smoke;

public class LoopbackFrameFactoryTests
{
    [Fact]
    public void TryCreateFrame_ShouldDownmixStereoFloatToMono()
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        var samples = new float[] { 1f, -1f, 0.5f, 0.5f };
        var buffer = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, buffer, 0, buffer.Length);

        var frame = LoopbackFrameFactory.TryCreateFrame(buffer, buffer.Length, waveFormat, 48_000);

        Assert.NotNull(frame);
        Assert.Equal(2, frame!.SamplesMono.Length);
        Assert.Equal(0f, frame.SamplesMono[0], 3);
        Assert.Equal(0.5f, frame.SamplesMono[1], 3);
    }

    [Fact]
    public void TryCreateFrame_ShouldResampleToTargetRate()
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44_100, 1);
        var samples = Enumerable.Range(0, 441).Select(i => MathF.Sin(i / 10f)).ToArray();
        var buffer = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, buffer, 0, buffer.Length);

        var frame = LoopbackFrameFactory.TryCreateFrame(buffer, buffer.Length, waveFormat, 48_000);

        Assert.NotNull(frame);
        Assert.Equal(480, frame!.SamplesMono.Length);
    }
}
