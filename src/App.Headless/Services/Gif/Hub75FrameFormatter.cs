using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace App.Headless.Services.Gif;

internal sealed class Hub75FrameFormatter
{
    public RgbaColor[] Format(DecodedGifFrame sourceFrame, GifScaleMode scaleMode)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);
        if (sourceFrame.Width <= 0 || sourceFrame.Height <= 0)
        {
            throw new InvalidDataException("Frame GIF invalido: dimensoes devem ser maiores que zero.");
        }

        var expectedPixels = sourceFrame.Width * sourceFrame.Height;
        if (sourceFrame.Pixels.Length != expectedPixels)
        {
            throw new InvalidDataException("Frame GIF invalido: quantidade de pixels inconsistente.");
        }

        var target = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        Array.Fill(target, new RgbaColor(0, 0, 0, 255));

        if (scaleMode == GifScaleMode.Stretch)
        {
            BlitScaled(sourceFrame.Pixels, sourceFrame.Width, sourceFrame.Height, target, 0f, 0f, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
            return target;
        }

        var widthScale = LedDefaults.MatrixWidth / (float)sourceFrame.Width;
        var heightScale = LedDefaults.MatrixHeight / (float)sourceFrame.Height;
        var scale = scaleMode == GifScaleMode.Fill
            ? MathF.Max(widthScale, heightScale)
            : MathF.Min(widthScale, heightScale);
        scale = MathF.Max(scale, 0.0001f);

        var drawWidth = Math.Max(1, (int)MathF.Round(sourceFrame.Width * scale));
        var drawHeight = Math.Max(1, (int)MathF.Round(sourceFrame.Height * scale));
        var offsetX = (LedDefaults.MatrixWidth - drawWidth) / 2f;
        var offsetY = (LedDefaults.MatrixHeight - drawHeight) / 2f;

        BlitScaled(sourceFrame.Pixels, sourceFrame.Width, sourceFrame.Height, target, offsetX, offsetY, drawWidth, drawHeight);
        return target;
    }

    private static void BlitScaled(
        RgbaColor[] sourcePixels,
        int sourceWidth,
        int sourceHeight,
        RgbaColor[] targetPixels,
        float drawOffsetX,
        float drawOffsetY,
        int drawWidth,
        int drawHeight)
    {
        for (var targetY = 0; targetY < LedDefaults.MatrixHeight; targetY++)
        {
            var localY = targetY - drawOffsetY;
            if (localY < 0f || localY >= drawHeight)
            {
                continue;
            }

            var sourceY = Math.Clamp(
                (int)MathF.Floor((localY * sourceHeight) / drawHeight),
                0,
                sourceHeight - 1);

            for (var targetX = 0; targetX < LedDefaults.MatrixWidth; targetX++)
            {
                var localX = targetX - drawOffsetX;
                if (localX < 0f || localX >= drawWidth)
                {
                    continue;
                }

                var sourceX = Math.Clamp(
                    (int)MathF.Floor((localX * sourceWidth) / drawWidth),
                    0,
                    sourceWidth - 1);

                var sourceIndex = (sourceY * sourceWidth) + sourceX;
                var targetIndex = (targetY * LedDefaults.MatrixWidth) + targetX;
                targetPixels[targetIndex] = sourcePixels[sourceIndex];
            }
        }
    }
}
