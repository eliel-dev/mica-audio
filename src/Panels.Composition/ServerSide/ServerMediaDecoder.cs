using ImageMagick;
using MicaAudio.Core.Presets;

namespace Panels.Composition.ServerSide;

// Cross-platform media decoder for server-side widget rendering (GIF, PNG, JPG, BMP).
// Used by ServerGifWidgetRuntime to load media uploaded via PUT /api/v1/admin/devices/{id}/media.
// Pre-scales each frame to the widget's target dimensions so the render hot-path is
// allocation-free (just a pixel copy at the right position).
//
// Scaling modes mirror the WinUI GifScaleMode:
//   Fit     — maintain aspect ratio, letter/pillarbox with black
//   Fill    — crop-to-fill, no black bars (WinUI default for GIF)
//   Stretch — distort to fill widget exactly
public enum MediaScaleMode { Fit, Fill, Stretch }

public sealed record DecodedMediaFrame(
    RgbaColor[] Pixels,
    int FrameWidth,
    int FrameHeight,
    int DurationMs = ServerMediaDecoder.DefaultFrameDurationMs);

public sealed class MediaSequence
{
    public static readonly MediaSequence Empty = new(Array.Empty<DecodedMediaFrame>());

    public IReadOnlyList<DecodedMediaFrame> Frames { get; }

    public bool IsEmpty => Frames.Count == 0;

    public MediaSequence(IReadOnlyList<DecodedMediaFrame> frames)
    {
        Frames = frames;
    }
}

public static class ServerMediaDecoder
{
    public const int DefaultFrameDurationMs = 100;
    public const int MaxGifFrames = 720;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp" };

    /// <summary>
    /// Decodes a media file and pre-scales every frame to (targetWidth × targetHeight).
    /// Returns <see cref="MediaSequence.Empty"/> when the file does not exist, the
    /// extension is unsupported, or decoding fails.
    /// </summary>
    public static MediaSequence DecodeFile(
        string filePath,
        int targetWidth,
        int targetHeight,
        MediaScaleMode scaleMode = MediaScaleMode.Fit)
    {
        if (!File.Exists(filePath))
        {
            return MediaSequence.Empty;
        }

        var ext = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(ext))
        {
            return MediaSequence.Empty;
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            return DecodeBytes(bytes, targetWidth, targetHeight, scaleMode,
                isGif: ext.Equals(".gif", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is InvalidDataException or MagickException or IOException)
        {
            return MediaSequence.Empty;
        }
    }

    private static MediaSequence DecodeBytes(
        byte[] bytes,
        int targetWidth,
        int targetHeight,
        MediaScaleMode scaleMode,
        bool isGif)
    {
        if (bytes.Length == 0)
        {
            return MediaSequence.Empty;
        }

        if (isGif)
        {
            ValidateGifSignature(bytes);
        }

        using var collection = new MagickImageCollection();
        collection.Read(bytes);
        if (collection.Count == 0)
        {
            return MediaSequence.Empty;
        }

        if (isGif && collection.Count > 1)
        {
            collection.Coalesce();
        }

        var frameCount = Math.Min(collection.Count, MaxGifFrames);
        var target = Math.Max(1, targetWidth);
        var targetH = Math.Max(1, targetHeight);

        var frames = new List<DecodedMediaFrame>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            var magickFrame = collection[i];
            var durationMs = ResolveDurationMs(magickFrame, isGif);

            // Clone and scale in one step using Magick's native operations.
            using var scaled = magickFrame.Clone();
            ApplyScale(scaled, target, targetH, scaleMode);

            var pixels = ExtractRgba(scaled, target, targetH);
            frames.Add(new DecodedMediaFrame(pixels, target, targetH, durationMs));
        }

        return frames.Count == 0 ? MediaSequence.Empty : new MediaSequence(frames);
    }

    private static void ApplyScale(IMagickImage<byte> image, int targetWidth, int targetHeight, MediaScaleMode mode)
    {
        var targetGeometry = new MagickGeometry((uint)targetWidth, (uint)targetHeight);

        switch (mode)
        {
            case MediaScaleMode.Stretch:
                targetGeometry.IgnoreAspectRatio = true;
                image.Resize(targetGeometry);
                break;

            case MediaScaleMode.Fill:
                // Scale to fill the target bounding box, then crop the excess.
                targetGeometry.FillArea = true;
                image.Resize(targetGeometry);
                if ((int)image.Width != targetWidth || (int)image.Height != targetHeight)
                {
                    var offsetX = ((int)image.Width - targetWidth) / 2;
                    var offsetY = ((int)image.Height - targetHeight) / 2;
                    image.Crop(new MagickGeometry(
                        Math.Max(0, offsetX),
                        Math.Max(0, offsetY),
                        (uint)targetWidth,
                        (uint)targetHeight));
                    image.ResetPage();
                }
                break;

            case MediaScaleMode.Fit:
            default:
                // Maintain aspect ratio; pad with black to reach exact target size.
                targetGeometry.IgnoreAspectRatio = false;
                image.Thumbnail(targetGeometry);

                // Pad to exact dimensions when aspect ratio left black bars.
                if ((int)image.Width != targetWidth || (int)image.Height != targetHeight)
                {
                    image.Extent((uint)targetWidth, (uint)targetHeight, Gravity.Center,
                        MagickColor.FromRgb(0, 0, 0));
                }
                break;
        }
    }

    private static RgbaColor[] ExtractRgba(IMagickImage<byte> image, int expectedWidth, int expectedHeight)
    {
        var output = new RgbaColor[expectedWidth * expectedHeight];
        using var pixels = image.GetPixels();
        var byteArray = pixels.ToByteArray(PixelMapping.RGBA);
        if (byteArray is null || byteArray.Length < output.Length * 4)
        {
            return output; // Return black-transparent on failure.
        }

        for (var i = 0; i < output.Length; i++)
        {
            var offset = i * 4;
            output[i] = new RgbaColor(byteArray[offset], byteArray[offset + 1], byteArray[offset + 2], byteArray[offset + 3]);
        }

        return output;
    }

    private static int ResolveDurationMs(IMagickImage<byte> image, bool isGif)
    {
        if (!isGif)
        {
            return DefaultFrameDurationMs;
        }

        var ticksPerSecond = image.AnimationTicksPerSecond > 0
            ? (double)image.AnimationTicksPerSecond
            : 100d;
        var durationMs = (int)Math.Round((image.AnimationDelay * 1000d) / ticksPerSecond, MidpointRounding.AwayFromZero);
        return durationMs > 0 ? durationMs : DefaultFrameDurationMs;
    }

    private static void ValidateGifSignature(byte[] data)
    {
        if (data.Length < 6
            || data[0] != (byte)'G'
            || data[1] != (byte)'I'
            || data[2] != (byte)'F'
            || data[3] != (byte)'8'
            || (data[4] != (byte)'7' && data[4] != (byte)'9')
            || data[5] != (byte)'a')
        {
            throw new InvalidDataException("Arquivo invalido: esperado GIF87a/GIF89a.");
        }
    }
}
