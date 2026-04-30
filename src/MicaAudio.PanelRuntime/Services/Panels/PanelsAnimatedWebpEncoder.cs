using ImageMagick;
using ImageMagick.Formats;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#transporte-em-lotes-webp
// DOCS: docs/handoffs/2026-04-18-panels-webp-batch-pipeline-optimizations.md
internal static class PanelsAnimatedWebpEncoder
{
    public static PanelsEncodedBatch Encode(
        IReadOnlyList<RgbaColor[]> frames,
        IReadOnlyList<int> frameDurationsMs,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(frameDurationsMs);

        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        if (frames.Count != frameDurationsMs.Count)
        {
            throw new ArgumentException("Frame durations must match the frame count.", nameof(frameDurationsMs));
        }

        return Encode(
            frames.Count,
            width,
            height,
            (frameIndex, targetFrame) =>
            {
                var sourceFrame = frames[frameIndex];
                if (sourceFrame.Length != targetFrame.Length)
                {
                    throw new ArgumentException(
                        $"Frame {frameIndex} has {sourceFrame.Length} pixels but expected {targetFrame.Length}.",
                        nameof(frames));
                }

                sourceFrame.CopyTo(targetFrame, 0);
            },
            frameIndex => frameDurationsMs[frameIndex]);
    }

    public static PanelsEncodedBatch Encode(
        int frameCount,
        int width,
        int height,
        Action<int, RgbaColor[]> renderFrame,
        Func<int, int> resolveFrameDurationMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(renderFrame);
        ArgumentNullException.ThrowIfNull(resolveFrameDurationMs);

        var pixelCount = width * height;
        var frameBuffer = new RgbaColor[pixelCount];
        var rgbaBytes = GC.AllocateUninitializedArray<byte>(pixelCount * 4);
        using var collection = new MagickImageCollection();
        var readSettings = new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA);
        var totalDurationMs = 0;

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            renderFrame(frameIndex, frameBuffer);
            CopyFrameToRgbaBytes(frameBuffer, rgbaBytes);
            var durationMs = Math.Max(1, resolveFrameDurationMs(frameIndex));
            totalDurationMs += durationMs;

            var image = new MagickImage(rgbaBytes, readSettings)
            {
                AnimationDelay = (uint)durationMs,
                AnimationTicksPerSecond = 1000,
                AnimationIterations = 1,
                Page = new MagickGeometry(0, 0, (uint)width, (uint)height),
            };

            collection.Add(image);
        }

        var writeDefines = new WebPWriteDefines
        {
            Lossless = true,
            Method = 6,
            ThreadLevel = true,
            UseSharpYuv = true,
        };

        using var ms = new MemoryStream(EstimateInitialCapacity(frameCount, width, height));
        collection.Write(ms, writeDefines);
        return new PanelsEncodedBatch(ms.ToArray(), frameCount, totalDurationMs);
    }

    private static void CopyFrameToRgbaBytes(RgbaColor[] frame, byte[] rgbaBytes)
    {
        for (var pixelIndex = 0; pixelIndex < frame.Length; pixelIndex++)
        {
            var pixel = frame[pixelIndex];
            var offset = pixelIndex * 4;
            rgbaBytes[offset] = pixel.R;
            rgbaBytes[offset + 1] = pixel.G;
            rgbaBytes[offset + 2] = pixel.B;
            rgbaBytes[offset + 3] = pixel.A;
        }
    }

    private static int EstimateInitialCapacity(int frameCount, int width, int height)
    {
        const int minCapacityBytes = 16 * 1024;
        const int maxCapacityBytes = 1024 * 1024;
        var estimated = frameCount * width * height / 2;
        return Math.Clamp(estimated, minCapacityBytes, maxCapacityBytes);
    }
}

internal sealed record PanelsEncodedBatch(byte[] Payload, int FrameCount, int DurationMs);
