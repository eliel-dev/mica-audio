using Audio.Loopback.Capture;

namespace Integration.Smoke;

public class LoopbackCaptureRuntimeConfigTests
{
    [Fact]
    public void From_ShouldClampChannelCapacityAndBufferMilliseconds()
    {
        var runtimeConfig = LoopbackCaptureRuntimeConfig.From(new CaptureConfig
        {
            ChannelCapacity = 1,
            BufferMilliseconds = 2,
            TargetSampleRate = 48_000,
        });

        Assert.Equal(2, runtimeConfig.ChannelCapacity);
        Assert.Equal(8, runtimeConfig.BufferMilliseconds);
        Assert.Equal(48_000, runtimeConfig.TargetSampleRate);
    }
}
