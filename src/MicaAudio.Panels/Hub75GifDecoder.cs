using ImageMagick;
using MicaAudio.Core.Presets;

namespace MicaAudio.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-compartilhado
public sealed class Hub75GifDecoder
{
    public const int DefaultMaxGifFrames = 720;
    public const int DefaultFrameDurationMs = 100;

    private readonly int maxGifFrames;

    public Hub75GifDecoder(int maxGifFrames = DefaultMaxGifFrames)
    {
        this.maxGifFrames = Math.Max(1, maxGifFrames);
    }

    public IReadOnlyList<DecodedGifFrame> Decode(byte[] gifBytes, CancellationToken cancellationToken = default)
    {
        using var collection = ReadCoalescedFrames(gifBytes);
        var frameCount = Math.Min(collection.Count, maxGifFrames);

        var output = new List<DecodedGifFrame>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = collection[i];
            output.Add(new DecodedGifFrame((int)frame.Width, (int)frame.Height, CopyPixels(frame), ResolveFrameDurationMs(frame)));
        }

        return output;
    }

    public static DecodedGifFrame DecodeFirstFrame(byte[] gifBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var collection = ReadCoalescedFrames(gifBytes);
        var frame = collection[0];
        return new DecodedGifFrame((int)frame.Width, (int)frame.Height, CopyPixels(frame), ResolveFrameDurationMs(frame));
    }

    public static DecodedGifFrame DecodeStaticImage(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(imageBytes);
        return new DecodedGifFrame((int)image.Width, (int)image.Height, CopyPixels(image), DefaultFrameDurationMs);
    }

    private static MagickImageCollection ReadCoalescedFrames(byte[] gifBytes)
    {
        ValidateGifSignature(gifBytes);

        var collection = new MagickImageCollection();
        collection.Read(gifBytes);
        if (collection.Count == 0)
        {
            collection.Dispose();
            throw new InvalidDataException("GIF sem frames decodificaveis.");
        }

        collection.Coalesce();
        return collection;
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

    private static int ResolveFrameDurationMs(IMagickImage<byte> image)
    {
        var ticksPerSecond = image.AnimationTicksPerSecond > 0
            ? (double)image.AnimationTicksPerSecond
            : 100d;
        var durationMs = (int)Math.Round((image.AnimationDelay * 1000d) / ticksPerSecond, MidpointRounding.AwayFromZero);
        return durationMs > 0
            ? durationMs
            : DefaultFrameDurationMs;
    }

    private static RgbaColor[] CopyPixels(IMagickImage<byte> image)
    {
        var pixels = image.GetPixels();
        var bytes = pixels.ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidDataException("Falha ao extrair pixels RGBA.");
        var output = new RgbaColor[(int)(image.Width * image.Height)];
        var expectedByteCount = output.Length * 4;
        if (bytes.Length < expectedByteCount)
        {
            throw new InvalidDataException("Imagem retornou menos bytes RGBA do que o esperado.");
        }

        for (var pixelIndex = 0; pixelIndex < output.Length; pixelIndex++)
        {
            var offset = pixelIndex * 4;
            output[pixelIndex] = new RgbaColor(
                bytes[offset],
                bytes[offset + 1],
                bytes[offset + 2],
                bytes[offset + 3]);
        }

        return output;
    }
}

public sealed record DecodedGifFrame(int Width, int Height, RgbaColor[] Pixels, int DurationMs = Hub75GifDecoder.DefaultFrameDurationMs);
