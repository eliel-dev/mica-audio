using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

[SupportedOSPlatform("windows")]
public sealed class GifCatalogAppRuntimeServiceTests
{
    [Fact]
    public async Task StartFromFileAsync_ShouldRespectDecoderFrameCap()
    {
        var matrix = new RecordingLedOutput();
        var simulator = new RecordingLedOutput();
        using var service = new GifCatalogAppRuntimeService(
            matrixOutput: matrix,
            simulatorOutput: simulator,
            decoder: new Hub75GifDecoder(maxGifFrames: 1),
            formatter: new Hub75FrameFormatter(),
            player: new Hub75GifPlayer(TimeSpan.FromMilliseconds(1000d / GifCatalogAppRuntimeService.TargetFps)));

        var path = WriteTempGifFile(CreateAnimatedGifBytes(frameCount: 2));
        try
        {
            await service.StartFromFileAsync(path, GifScaleMode.Fit);
            Assert.Equal(1, service.LoadedFrameCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Stop_ShouldSendLegacyZeroBinsAfterFrameMode()
    {
        var matrix = new RecordingLedOutput();
        var simulator = new RecordingLedOutput();
        using var service = new GifCatalogAppRuntimeService(
            matrixOutput: matrix,
            simulatorOutput: simulator,
            decoder: new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames),
            formatter: new Hub75FrameFormatter(),
            player: new Hub75GifPlayer(TimeSpan.FromMilliseconds(1000d / GifCatalogAppRuntimeService.TargetFps)));

        var path = WriteTempGifFile(CreateSingleFrameGifBytes());
        try
        {
            await service.StartFromFileAsync(path, GifScaleMode.Fit);
            service.Stop();

            Assert.True(matrix.Sent.Count >= 2);
            Assert.Contains(matrix.Sent, static payload => payload.Frame128x64 is { Length: > 0 });

            var binsPayload = matrix.Sent[^1];
            Assert.NotNull(binsPayload.Bins128);
            Assert.All(binsPayload.Bins128!, value => Assert.Equal(0f, value));
            Assert.Null(binsPayload.Frame128x64);

            var simulatorLast = simulator.Sent[^1];
            Assert.NotNull(simulatorLast.Frame128x64);
            Assert.All(simulatorLast.Frame128x64!, px => Assert.Equal(new RgbaColor(0, 0, 0, 255), px));
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public async Task Stop_ShouldNotThrow_WhenFrameUpdatedSubscriberThrows()
    {
        using var service = CreateService();

        var path = WriteTempGifFile(CreateSingleFrameGifBytes());
        try
        {
            await service.StartFromFileAsync(path, GifScaleMode.Fit);
            service.FrameUpdated += static (_, _) => throw new InvalidCastException("simulated callback failure");

            var ex = Record.Exception(service.Stop);
            Assert.Null(ex);
        }
        finally
        {
            File.Delete(path);
        }
    }
    private static GifCatalogAppRuntimeService CreateService(
        int maxDownloadBytes = GifCatalogAppRuntimeService.MaxDownloadBytes)
    {
        return new GifCatalogAppRuntimeService(
            matrixOutput: new RecordingLedOutput(),
            simulatorOutput: new RecordingLedOutput(),
            decoder: new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames),
            formatter: new Hub75FrameFormatter(),
            player: new Hub75GifPlayer(TimeSpan.FromMilliseconds(1000d / GifCatalogAppRuntimeService.TargetFps)),
            maxDownloadBytes: maxDownloadBytes);
    }

    private static string WriteTempGifFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.gif");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] CreateSingleFrameGifBytes()
    {
        return Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");
    }

    private static byte[] CreateAnimatedGifBytes(int frameCount)
    {
        using var firstFrame = new Bitmap(2, 2);
        using (var g = Graphics.FromImage(firstFrame))
        {
            g.Clear(Color.Red);
        }

        var encoder = ImageCodecInfo.GetImageDecoders().First(codec => codec.FormatID == ImageFormat.Gif.Guid);
        using var stream = new MemoryStream();
        using var saveEncoder = new EncoderParameters(1);
        saveEncoder.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        firstFrame.Save(stream, encoder, saveEncoder);

        for (var i = 1; i < Math.Max(1, frameCount); i++)
        {
            using var frame = new Bitmap(2, 2);
            using (var g = Graphics.FromImage(frame))
            {
                g.Clear(i % 2 == 0 ? Color.Blue : Color.Green);
            }

            using var frameEncoder = new EncoderParameters(1);
            frameEncoder.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            firstFrame.SaveAdd(frame, frameEncoder);
        }

        using var flushEncoder = new EncoderParameters(1);
        flushEncoder.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
        firstFrame.SaveAdd(flushEncoder);

        return stream.ToArray();
    }

    private sealed class RecordingLedOutput : ILedOutput
    {
        public List<LedPayload> Sent { get; } = new();

        public bool IsAvailable => true;

        public void Start(LedOutputConfig config)
        {
        }

        public void Stop()
        {
        }

        public void Send(LedPayload payload)
        {
            Sent.Add(new LedPayload
            {
                Bins128 = payload.Bins128?.ToArray(),
                Frame128x64 = payload.Frame128x64?.ToArray(),
                Level = payload.Level,
                PresetId = payload.PresetId,
            });
        }

        public void SetBrightness(float value)
        {
        }
    }

}


