using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImage = System.Drawing.Image;
using FrameDimension = System.Drawing.Imaging.FrameDimension;
using ImageLockMode = System.Drawing.Imaging.ImageLockMode;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Rectangle = System.Drawing.Rectangle;
using System.Runtime.Versioning;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Gif;

// DOCS: docs/wiki/guides/load-gif-hub75.md#passos
[SupportedOSPlatform("windows")]
internal sealed class Hub75GifDecoder
{
    public const int DefaultMaxGifFrames = 720;

    private readonly int maxGifFrames;

    public Hub75GifDecoder(int maxGifFrames = DefaultMaxGifFrames)
    {
        this.maxGifFrames = Math.Max(1, maxGifFrames);
    }

    public IReadOnlyList<DecodedGifFrame> Decode(byte[] gifBytes, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeGif(gifBytes))
        {
            throw new InvalidDataException("Arquivo invalido: esperado GIF87a/GIF89a.");
        }

        using var input = new MemoryStream(gifBytes, writable: false);
        using var image = DrawingImage.FromStream(input, useEmbeddedColorManagement: false, validateImageData: true);

        var frameDimension = ResolveFrameDimension(image);
        var frameCount = Math.Min(image.GetFrameCount(frameDimension), maxGifFrames);
        if (frameCount <= 0)
        {
            throw new InvalidDataException("GIF sem frames decodificaveis.");
        }

        using var canvas = new DrawingBitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using var canvasGraphics = DrawingGraphics.FromImage(canvas);
        canvasGraphics.Clear(DrawingColor.Transparent);
        canvasGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

        var output = new List<DecodedGifFrame>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            image.SelectActiveFrame(frameDimension, i);
            canvasGraphics.DrawImage(image, 0, 0, image.Width, image.Height);

            using var snapshot = (DrawingBitmap)canvas.Clone();
            output.Add(new DecodedGifFrame(snapshot.Width, snapshot.Height, CopyPixels(snapshot)));
        }

        return output;
    }

    public static DecodedGifFrame DecodeFirstFrame(byte[] gifBytes, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeGif(gifBytes))
        {
            throw new InvalidDataException("Arquivo invalido: esperado GIF87a/GIF89a.");
        }

        using var input = new MemoryStream(gifBytes, writable: false);
        using var image = DrawingImage.FromStream(input, useEmbeddedColorManagement: false, validateImageData: true);

        var frameDimension = ResolveFrameDimension(image);
        if (image.GetFrameCount(frameDimension) <= 0)
        {
            throw new InvalidDataException("GIF sem frames decodificaveis.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        image.SelectActiveFrame(frameDimension, 0);
        using var snapshot = new DrawingBitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using var graphics = DrawingGraphics.FromImage(snapshot);
        graphics.Clear(DrawingColor.Transparent);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        return new DecodedGifFrame(snapshot.Width, snapshot.Height, CopyPixels(snapshot));
    }

    private static FrameDimension ResolveFrameDimension(DrawingImage image)
    {
        if (image.FrameDimensionsList.Length == 0)
        {
            return FrameDimension.Time;
        }

        var preferred = image.FrameDimensionsList.FirstOrDefault(id => id == FrameDimension.Time.Guid);
        return preferred != Guid.Empty
            ? new FrameDimension(preferred)
            : new FrameDimension(image.FrameDimensionsList[0]);
    }

    private static bool LooksLikeGif(byte[] data)
    {
        if (data.Length < 6)
        {
            return false;
        }

        return data[0] == (byte)'G'
            && data[1] == (byte)'I'
            && data[2] == (byte)'F'
            && data[3] == (byte)'8'
            && (data[4] == (byte)'7' || data[4] == (byte)'9')
            && data[5] == (byte)'a';
    }

    private static RgbaColor[] CopyPixels(DrawingBitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var bytes = new byte[stride * bitmap.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var output = new RgbaColor[bitmap.Width * bitmap.Height];
            for (var y = 0; y < bitmap.Height; y++)
            {
                var rowOffset = data.Stride >= 0
                    ? y * stride
                    : (bitmap.Height - 1 - y) * stride;

                for (var x = 0; x < bitmap.Width; x++)
                {
                    var i = (y * bitmap.Width) + x;
                    var pixelOffset = rowOffset + (x * 4);
                    var b = bytes[pixelOffset];
                    var g = bytes[pixelOffset + 1];
                    var r = bytes[pixelOffset + 2];
                    var a = bytes[pixelOffset + 3];
                    output[i] = new RgbaColor(r, g, b, a);
                }
            }

            return output;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}

internal sealed record DecodedGifFrame(int Width, int Height, RgbaColor[] Pixels);
