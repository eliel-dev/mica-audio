using ImageMagick;
using ImageMagick.Formats;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#transporte-em-lotes-webp
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

        var pixelCount = width * height;
        using var collection = new MagickImageCollection();
        var readSettings = new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA);

        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            if (frame.Length != pixelCount)
            {
                throw new ArgumentException($"Frame {i} has {frame.Length} pixels but expected {pixelCount}.", nameof(frames));
            }

            var rgbaBytes = new byte[pixelCount * 4];
            for (var pixelIndex = 0; pixelIndex < frame.Length; pixelIndex++)
            {
                var pixel = frame[pixelIndex];
                var offset = pixelIndex * 4;
                rgbaBytes[offset] = pixel.R;
                rgbaBytes[offset + 1] = pixel.G;
                rgbaBytes[offset + 2] = pixel.B;
                rgbaBytes[offset + 3] = pixel.A;
            }

            var image = new MagickImage(rgbaBytes, readSettings)
            {
                AnimationDelay = (uint)Math.Max(1, frameDurationsMs[i]),
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

        using var ms = new MemoryStream();
        collection.Write(ms, writeDefines);
        return new PanelsEncodedBatch(ms.ToArray(), frames.Count, frameDurationsMs.Sum());
    }
}

internal sealed record PanelsEncodedBatch(byte[] Payload, int FrameCount, int DurationMs);
