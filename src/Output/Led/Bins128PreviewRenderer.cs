using System.Diagnostics;
using Device.Protocol.Stream;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

// DOCS: docs/wiki/modules/output-led.md#atualizacao-2026-03-preview-hub75-local-fiel-ao-bins128-do-device
public sealed class Bins128PreviewRenderer
{
    private byte[] binsPeakHeights = new byte[64];
    private byte[] binsHistory = new byte[64 * 64];
    private int binsHistoryHead;
    private byte[] launchpadPadLevels = new byte[64];
    private byte[] launchpadTopLevels = new byte[8];
    private byte[] launchpadSideLevels = new byte[8];
    private Bins128VisualStyle? lastStyle;
    private int lastWidth = LedDefaults.MatrixWidth;
    private int lastHeight = LedDefaults.MatrixHeight;

    public void Reset()
    {
        Array.Clear(binsPeakHeights);
        Array.Clear(binsHistory);
        Array.Clear(launchpadPadLevels);
        Array.Clear(launchpadTopLevels);
        Array.Clear(launchpadSideLevels);
        binsHistoryHead = 0;
        lastStyle = null;
    }

    public void Render(float[] bins128, float level, byte binsFlags, int width, int height, float brightness, RgbaColor[] target)
    {
        ArgumentNullException.ThrowIfNull(bins128);
        ArgumentNullException.ThrowIfNull(target);

        if (bins128.Length != LedDefaults.MatrixWidth)
        {
            throw new ArgumentException("Bins128 preview renderer requires exactly 128 bins.", nameof(bins128));
        }

        if (target.Length != width * height)
        {
            throw new ArgumentException("Target buffer length must match width * height.", nameof(target));
        }

        EnsureState(width, height);

        var style = Bins128VisualFlags.GetStyle(binsFlags);
        var palette = Bins128VisualFlags.GetPalette(binsFlags);
        if (lastStyle != style)
        {
            Reset();
            lastStyle = style;
        }

        Span<byte> bins = stackalloc byte[LedDefaults.MatrixWidth];
        for (var i = 0; i < bins.Length; i++)
        {
            bins[i] = ToByte01(bins128[i]);
        }

        Array.Fill(target, new RgbaColor(0, 0, 0, 255));

        switch (style)
        {
            case Bins128VisualStyle.WaveMirror:
                DrawWaveMirror(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.MirrorLines:
                DrawMirrorLines(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.MirrorBlocks:
                DrawMirrorBlocks(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.ClassicBars:
                DrawClassicBars(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.FlowLine:
                DrawFlowLine(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.HistoryScan:
                DrawHistoryScan(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.RadialOrbit:
                DrawRadialOrbit(bins, palette, width, height, brightness, target);
                break;
            case Bins128VisualStyle.Atmosphere:
                DrawAtmosphere(bins, palette, level, width, height, brightness, target);
                break;
            case Bins128VisualStyle.LaunchpadGrid:
                DrawLaunchpadGrid(bins, width, height, brightness, target);
                break;
            case Bins128VisualStyle.LegacyFallback:
            default:
                DrawLegacyFallback(bins128, level, width, height, brightness, target);
                break;
        }
    }

    private void EnsureState(int width, int height)
    {
        if (lastWidth == width && lastHeight == height)
        {
            return;
        }

        lastWidth = width;
        lastHeight = height;
        binsPeakHeights = new byte[Math.Max(64, width)];
        binsHistory = new byte[Math.Max(1, height) * Math.Max(1, width / 2)];
        launchpadPadLevels = new byte[64];
        launchpadTopLevels = new byte[8];
        launchpadSideLevels = new byte[8];
        Reset();
    }

    private static void DrawLegacyFallback(float[] bins128, float level, int width, int height, float brightness, RgbaColor[] target)
    {
        var centerTop = (height - 1) / 2;
        var centerBottom = height / 2;
        var halfHeight = Math.Max(1, height / 2);
        var maxX = Math.Min(width, bins128.Length);

        for (var x = 0; x < maxX; x++)
        {
            var value = Clamp01(bins128[x]);
            var litHalf = (int)MathF.Round(value * halfHeight);
            if (litHalf <= 0)
            {
                continue;
            }

            for (var offset = 0; offset < litHalf; offset++)
            {
                var t = x / (float)Math.Max(1, maxX - 1);
                var color = ApplyBrightness(
                    new RgbaColor(
                        ClampToByte(65f + (170f * t)),
                        ClampToByte(150f + (95f * (1f - MathF.Abs((2f * t) - 1f)))),
                        ClampToByte(45f + (170f * (1f - t)) + (level * 28f))),
                    brightness);

                var yTop = centerTop - offset;
                if (yTop >= 0)
                {
                    target[(yTop * width) + x] = color;
                }

                var yBottom = centerBottom + offset;
                if (yBottom < height)
                {
                    target[(yBottom * width) + x] = color;
                }
            }
        }
    }

    private static void DrawWaveMirror(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.WaveMirror, palette);
        var midY = height / 2;
        for (var x = 0; x < width; x++)
        {
            var centerColor = SamplePaletteColor(effectivePalette, x / (float)Math.Max(1, width - 1));
            DrawPixel(x, midY, ScaleColor(centerColor, 0.70f), brightness, width, height, target);
        }

        var halfHeightLimit = Math.Max(1, midY - 1);
        for (var x = 0; x < width; x++)
        {
            var amplitude = SmoothBinsSample(bins, x);
            var halfHeight = AmplitudeToHeight(amplitude, (byte)halfHeightLimit);
            var color = SamplePaletteColor(effectivePalette, x / (float)Math.Max(1, width - 1));
            var glow = ScaleColor(color, 0.42f);
            for (var offset = 0; offset <= halfHeight; offset++)
            {
                var yTop = midY - offset;
                var yBottom = midY + offset;
                DrawPixel(x, yTop, color, brightness, width, height, target);
                DrawPixel(x, yBottom, color, brightness, width, height, target);
                if (offset > 0)
                {
                    DrawPixel(x - 1, yTop, glow, brightness, width, height, target);
                    DrawPixel(x - 1, yBottom, glow, brightness, width, height, target);
                    DrawPixel(x + 1, yTop, glow, brightness, width, height, target);
                    DrawPixel(x + 1, yBottom, glow, brightness, width, height, target);
                }
            }
        }
    }

    private static void DrawMirrorLines(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.MirrorLines, palette);
        var midY = height / 2;
        var halfHeightLimit = Math.Max(1, midY - 1);
        for (var x = 0; x < width; x++)
        {
            var amplitude = SmoothBinsSample(bins, x);
            var halfHeight = AmplitudeToHeight(amplitude, (byte)halfHeightLimit);
            var color = SamplePaletteColor(effectivePalette, x / (float)Math.Max(1, width - 1));
            for (var offset = 0; offset < halfHeight; offset++)
            {
                DrawPixel(x, midY - offset, color, brightness, width, height, target);
                DrawPixel(x, midY + offset, color, brightness, width, height, target);
            }
        }
    }

    private static void DrawMirrorBlocks(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.MirrorBlocks, palette);
        const int blockCount = 64;
        var horizon = (int)MathF.Round(height * 0.62f);
        horizon = Math.Clamp(horizon, 0, height - 1);

        for (var block = 0; block < blockCount; block++)
        {
            var x = block * 2;
            var amplitude = SampleBinsAverage(bins, block * 2, (block * 2) + 2);
            var topHeight = AmplitudeToHeight(amplitude, (byte)Math.Clamp(horizon, 0, 255));
            var reflectionHeight = (byte)(topHeight * 0.45f);
            var color = SamplePaletteColor(effectivePalette, block / (float)(blockCount - 1));
            FillRect(x, horizon - topHeight, 2, topHeight, color, brightness, width, height, target);
            FillRect(x, horizon, 2, reflectionHeight, ScaleColor(color, 0.38f), brightness, width, height, target);
        }
    }

    private void DrawClassicBars(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.ClassicBars, palette);
        const int barCount = 64;
        for (var bar = 0; bar < barCount; bar++)
        {
            var x = bar * 2;
            var amplitude = SampleBinsAverage(bins, bar * 2, (bar * 2) + 2);
            var barHeight = AmplitudeToHeight(amplitude, (byte)Math.Max(0, height - 1));
            var previousPeak = binsPeakHeights[bar];
            var peak = barHeight > previousPeak ? barHeight : (byte)(previousPeak > 0 ? previousPeak - 1 : 0);
            binsPeakHeights[bar] = peak;
            var color = SamplePaletteColor(effectivePalette, bar / (float)(barCount - 1));
            FillRect(x, height - barHeight, 2, barHeight, color, brightness, width, height, target);
            if (peak > 0)
            {
                FillRect(x, height - peak, 2, 1, ScaleColor(color, 0.95f), brightness, width, height, target);
            }
        }
    }

    private static void DrawFlowLine(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.FlowLine, palette);
        const int sampleCount = 64;
        var previousX = 0;
        var previousY = height - AmplitudeToHeight(SampleBinsAverage(bins, 0, 2), (byte)Math.Max(0, height - 1));

        for (var sample = 0; sample < sampleCount; sample++)
        {
            var x = sample * 2;
            var amplitude = SampleBinsAverage(bins, sample * 2, (sample * 2) + 2);
            var y = height - AmplitudeToHeight(amplitude, (byte)Math.Max(0, height - 2));
            var color = SamplePaletteColor(effectivePalette, sample / (float)(sampleCount - 1));
            DrawLine(previousX, previousY, x, y, color, brightness, width, height, target);
            if ((sample & 0x01) == 0)
            {
                FillRect(x, y, 2, height - y, ScaleColor(color, 0.22f), brightness, width, height, target);
            }

            previousX = x;
            previousY = y;
        }
    }

    private void DrawHistoryScan(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.HistoryScan, palette);
        var columnCount = width / 2;
        var rowOffset = binsHistoryHead * columnCount;
        for (var column = 0; column < columnCount; column++)
        {
            binsHistory[rowOffset + column] = SampleBinsAverage(bins, column * 2, (column * 2) + 2);
        }

        binsHistoryHead = (binsHistoryHead + 1) % height;

        for (var row = 0; row < height; row++)
        {
            var historyIndex = (binsHistoryHead - 1 - row + height) % height;
            var historyOffset = historyIndex * columnCount;
            for (var column = 0; column < columnCount; column++)
            {
                var amplitude = binsHistory[historyOffset + column];
                if (amplitude < 6)
                {
                    continue;
                }

                var paletteT = Clamp01((amplitude / 255f) * 0.85f + (column / (float)Math.Max(1, columnCount - 1)) * 0.15f);
                var color = SamplePaletteColor(effectivePalette, paletteT);
                FillRect(column * 2, height - 1 - row, 2, 1, color, brightness, width, height, target);
            }
        }
    }

    private static void DrawRadialOrbit(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.RadialOrbit, palette);
        var centerX = width / 2;
        var centerY = height / 2;
        const int rayCount = 48;
        const float baseRadius = 7f;
        const float maxRadius = 28f;

        for (var ray = 0; ray < rayCount; ray++)
        {
            var binStart = (ray * LedDefaults.MatrixWidth) / rayCount;
            var binEnd = ((ray + 1) * LedDefaults.MatrixWidth) / rayCount;
            var amplitude = SampleBinsAverage(bins, binStart, binEnd);
            var angle = (ray / (float)rayCount) * 6.2831853f - 1.5707963f;
            var radius = baseRadius + ((amplitude / 255f) * maxRadius);
            var endX = (int)MathF.Round(centerX + MathF.Cos(angle) * radius);
            var endY = (int)MathF.Round(centerY + MathF.Sin(angle) * radius);
            var color = SamplePaletteColor(effectivePalette, ray / (float)(rayCount - 1));
            DrawLine(centerX, centerY, endX, endY, color, brightness, width, height, target);
            FillRect(endX - 1, endY - 1, 2, 2, ScaleColor(color, 0.68f), brightness, width, height, target);
        }

        FillRect(centerX - 2, centerY - 2, 4, 4, ScaleColor(SamplePaletteColor(effectivePalette, 0.5f), 0.72f), brightness, width, height, target);
    }

    private static void DrawAtmosphere(ReadOnlySpan<byte> bins, Bins128PaletteFamily palette, float level, int width, int height, float brightness, RgbaColor[] target)
    {
        var effectivePalette = ResolveEffectivePalette(Bins128VisualStyle.Atmosphere, palette);
        const int sampleCount = 64;
        var time = Stopwatch.GetTimestamp() / (float)Stopwatch.Frequency;
        var globalLevel = Clamp01(level);

        for (var ribbon = 0; ribbon < 3; ribbon++)
        {
            var baseline = height * (0.24f + (ribbon * 0.22f));
            var phase = time * (0.9f + (ribbon * 0.22f));
            var previousX = 0;
            var previousY = (int)MathF.Round(baseline);
            for (var sample = 0; sample < sampleCount; sample++)
            {
                var x = sample * 2;
                var amplitude = SampleBinsAverage(bins, sample * 2, (sample * 2) + 2);
                var energy = amplitude / 255f;
                var wave = MathF.Sin((sample * 0.22f) + phase + ribbon) * (5f + (globalLevel * 4f));
                var y = (int)MathF.Round(baseline + wave - (energy * 16f));
                var colorT = Clamp01((sample / (float)(sampleCount - 1)) * 0.65f + (ribbon * 0.18f));
                var color = SamplePaletteColor(effectivePalette, colorT);
                DrawLine(previousX, previousY, x, y, color, brightness, width, height, target);
                if ((sample % 6) == 0)
                {
                    FillRect(x - 1, y - 1, 3, 3, ScaleColor(color, 0.52f + (energy * 0.30f)), brightness, width, height, target);
                }

                previousX = x;
                previousY = y;
            }
        }

        var bloom = SamplePaletteColor(effectivePalette, 0.35f + (globalLevel * 0.25f));
        FillRect((width / 2) - 6, (height / 2) - 4, 12, 8, ScaleColor(bloom, 0.30f + (globalLevel * 0.30f)), brightness, width, height, target);
    }

    private void DrawLaunchpadGrid(ReadOnlySpan<byte> bins, int width, int height, float brightness, RgbaColor[] target)
    {
        var deviceBody = new RgbaColor(10, 12, 16);
        var padOff = new RgbaColor(34, 38, 46);
        var topOff = new RgbaColor(58, 64, 72);
        FillRect(18, 4, 92, 56, deviceBody, brightness, width, height, target);

        const int gridSize = 8;
        const int padWidth = 9;
        const int padHeight = 5;
        const int padGapX = 2;
        const int padGapY = 1;
        const int gridLeft = 22;
        const int gridTop = 12;

        for (var row = 0; row < gridSize; row++)
        {
            for (var col = 0; col < gridSize; col++)
            {
                var padIndex = (row * gridSize) + col;
                var targetLevel = SampleBinsAverage(bins, padIndex * 2, (padIndex * 2) + 2);
                var previous = launchpadPadLevels[padIndex];
                var levelValue = targetLevel > previous ? targetLevel : (byte)(previous > 14 ? previous - 14 : 0);
                launchpadPadLevels[padIndex] = levelValue;
                var x = gridLeft + (col * (padWidth + padGapX));
                var y = gridTop + (row * (padHeight + padGapY));
                FillRect(x, y, padWidth, padHeight, padOff, brightness, width, height, target);
                if (levelValue > 6)
                {
                    var colorT = Clamp01((col / 7f) * 0.55f + (row / 7f) * 0.25f + (levelValue / 255f) * 0.20f);
                    var activeColor = SamplePaletteColor(Bins128PaletteFamily.Neon, colorT);
                    FillRect(x + 1, y + 1, padWidth - 2, padHeight - 2, ScaleColor(activeColor, 0.35f + (levelValue / 255f) * 0.65f), brightness, width, height, target);
                }
            }
        }

        for (var col = 0; col < gridSize; col++)
        {
            var targetLevel = SampleBinsAverage(bins, col * 4, (col * 4) + 4);
            var previous = launchpadTopLevels[col];
            launchpadTopLevels[col] = targetLevel > previous ? targetLevel : (byte)(previous > 18 ? previous - 18 : 0);
            var x = gridLeft + (col * (padWidth + padGapX)) + 2;
            FillRect(x, 7, padWidth - 4, 3, topOff, brightness, width, height, target);
            if (launchpadTopLevels[col] > 12)
            {
                var color = SamplePaletteColor(Bins128PaletteFamily.Plasma, col / 7f);
                FillRect(x, 7, padWidth - 4, 3, ScaleColor(color, 0.28f + (launchpadTopLevels[col] / 255f) * 0.72f), brightness, width, height, target);
            }
        }

        for (var row = 0; row < gridSize; row++)
        {
            var targetLevel = SampleBinsAverage(bins, row * 4, (row * 4) + 4);
            var previous = launchpadSideLevels[row];
            launchpadSideLevels[row] = targetLevel > previous ? targetLevel : (byte)(previous > 18 ? previous - 18 : 0);
            var y = gridTop + (row * (padHeight + padGapY)) + 1;
            FillRect(112, y, 3, padHeight - 2, topOff, brightness, width, height, target);
            if (launchpadSideLevels[row] > 12)
            {
                var color = SamplePaletteColor(Bins128PaletteFamily.Aurora, row / 7f);
                FillRect(112, y, 3, padHeight - 2, ScaleColor(color, 0.28f + (launchpadSideLevels[row] / 255f) * 0.72f), brightness, width, height, target);
            }
        }
    }

    private static Bins128PaletteFamily ResolveEffectivePalette(Bins128VisualStyle style, Bins128PaletteFamily requestedPalette)
    {
        if (requestedPalette != Bins128PaletteFamily.Canonical)
        {
            return requestedPalette;
        }

        return style switch
        {
            Bins128VisualStyle.WaveMirror => Bins128PaletteFamily.Rainbow,
            Bins128VisualStyle.RadialOrbit => Bins128PaletteFamily.Mono,
            Bins128VisualStyle.Atmosphere => Bins128PaletteFamily.Aurora,
            Bins128VisualStyle.LaunchpadGrid => Bins128PaletteFamily.Canonical,
            _ => Bins128PaletteFamily.Rainbow,
        };
    }

    private static RgbaColor SamplePaletteColor(Bins128PaletteFamily palette, float t)
    {
        ReadOnlySpan<RgbaColor> stops = palette switch
        {
            Bins128PaletteFamily.Sunset =>
            [
                new RgbaColor(255, 72, 40),
                new RgbaColor(255, 132, 32),
                new RgbaColor(255, 188, 48),
                new RgbaColor(220, 82, 196),
                new RgbaColor(98, 54, 255),
            ],
            Bins128PaletteFamily.Arctic =>
            [
                new RgbaColor(24, 214, 255),
                new RgbaColor(0, 168, 255),
                new RgbaColor(74, 224, 255),
                new RgbaColor(138, 255, 214),
                new RgbaColor(114, 92, 255),
            ],
            Bins128PaletteFamily.Neon =>
            [
                new RgbaColor(57, 255, 20),
                new RgbaColor(0, 255, 180),
                new RgbaColor(0, 220, 255),
                new RgbaColor(120, 86, 255),
                new RgbaColor(255, 60, 180),
            ],
            Bins128PaletteFamily.Aurora =>
            [
                new RgbaColor(48, 255, 170),
                new RgbaColor(0, 222, 255),
                new RgbaColor(88, 124, 255),
                new RgbaColor(198, 84, 255),
                new RgbaColor(255, 176, 226),
            ],
            Bins128PaletteFamily.Plasma =>
            [
                new RgbaColor(255, 88, 46),
                new RgbaColor(255, 168, 26),
                new RgbaColor(255, 58, 168),
                new RgbaColor(64, 108, 255),
                new RgbaColor(72, 244, 255),
            ],
            Bins128PaletteFamily.Mono =>
            [
                new RgbaColor(88, 98, 118),
                new RgbaColor(180, 192, 214),
                new RgbaColor(255, 255, 255),
            ],
            Bins128PaletteFamily.Canonical => [new RgbaColor(255, 255, 255)],
            _ => [],
        };

        if (palette == Bins128PaletteFamily.Rainbow)
        {
            return RainbowColorForColumn((ushort)ClampToByte(MathF.Round(Clamp01(t) * 255f)), 256);
        }

        return SampleGradientStops(stops, t);
    }

    private static RgbaColor RainbowColorForColumn(ushort column, ushort columnCount)
    {
        if (columnCount <= 1)
        {
            return new RgbaColor(255, 0, 0);
        }

        var hue = (byte)((column * 255u) / (columnCount - 1u));
        var region = hue / 43u;
        var remainder = (byte)((hue - (region * 43u)) * 6u);
        var q = (byte)(255u - remainder);
        var tt = remainder;

        return region switch
        {
            0 => new RgbaColor(255, tt, 0),
            1 => new RgbaColor(q, 255, 0),
            2 => new RgbaColor(0, 255, tt),
            3 => new RgbaColor(0, q, 255),
            4 => new RgbaColor(tt, 0, 255),
            _ => new RgbaColor(255, 0, q),
        };
    }

    private static RgbaColor SampleGradientStops(ReadOnlySpan<RgbaColor> stops, float t)
    {
        if (stops.Length == 0)
        {
            return new RgbaColor(255, 255, 255);
        }

        if (stops.Length == 1)
        {
            return stops[0];
        }

        var clamped = Clamp01(t);
        var scaled = clamped * (stops.Length - 1);
        var leftIndex = (int)scaled;
        var rightIndex = leftIndex + 1 >= stops.Length ? stops.Length - 1 : leftIndex + 1;
        return MixColor(stops[leftIndex], stops[rightIndex], scaled - leftIndex);
    }

    private static RgbaColor MixColor(RgbaColor left, RgbaColor right, float amount)
    {
        var t = Clamp01(amount);
        return new RgbaColor(
            ClampToByte(MathF.Round(left.R + ((right.R - left.R) * t))),
            ClampToByte(MathF.Round(left.G + ((right.G - left.G) * t))),
            ClampToByte(MathF.Round(left.B + ((right.B - left.B) * t))),
            255);
    }

    private static RgbaColor ScaleColor(RgbaColor color, float amount)
    {
        var scale = Clamp01(amount);
        return new RgbaColor(
            ClampToByte(MathF.Round(color.R * scale)),
            ClampToByte(MathF.Round(color.G * scale)),
            ClampToByte(MathF.Round(color.B * scale)),
            color.A);
    }

    private static byte SmoothBinsSample(ReadOnlySpan<byte> bins, int index)
    {
        var clampedIndex = Math.Clamp(index, 0, bins.Length - 1);
        var leftIndex = clampedIndex > 0 ? clampedIndex - 1 : clampedIndex;
        var rightIndex = clampedIndex + 1 < bins.Length ? clampedIndex + 1 : clampedIndex;
        var smoothed = bins[leftIndex] + (bins[clampedIndex] * 2) + bins[rightIndex];
        return (byte)(smoothed / 4);
    }

    private static byte AmplitudeToHeight(byte amplitude, byte maxHeight)
    {
        return (byte)(((ushort)amplitude * maxHeight + 254u) / 255u);
    }

    private static byte SampleBinsAverage(ReadOnlySpan<byte> bins, int startInclusive, int endExclusive)
    {
        if (startInclusive >= endExclusive || startInclusive >= bins.Length)
        {
            return 0;
        }

        var safeEnd = Math.Min(endExclusive, bins.Length);
        uint sum = 0;
        for (var index = startInclusive; index < safeEnd; index++)
        {
            sum += bins[index];
        }

        return (byte)(sum / (uint)(safeEnd - startInclusive));
    }

    private static void DrawPixel(int x, int y, RgbaColor color, float brightness, int width, int height, RgbaColor[] target)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            return;
        }

        target[(y * width) + x] = ApplyBrightness(color, brightness);
    }

    private static void FillRect(int x, int y, int w, int h, RgbaColor color, float brightness, int width, int height, RgbaColor[] target)
    {
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var xStart = Math.Max(0, x);
        var yStart = Math.Max(0, y);
        var xEnd = Math.Min(width, x + w);
        var yEnd = Math.Min(height, y + h);
        if (xStart >= xEnd || yStart >= yEnd)
        {
            return;
        }

        var adjusted = ApplyBrightness(color, brightness);
        for (var py = yStart; py < yEnd; py++)
        {
            var rowOffset = py * width;
            for (var px = xStart; px < xEnd; px++)
            {
                target[rowOffset + px] = adjusted;
            }
        }
    }

    private static void DrawLine(int x0, int y0, int x1, int y1, RgbaColor color, float brightness, int width, int height, RgbaColor[] target)
    {
        var adjusted = ApplyBrightness(color, brightness);
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            if ((uint)x0 < (uint)width && (uint)y0 < (uint)height)
            {
                target[(y0 * width) + x0] = adjusted;
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = err * 2;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static RgbaColor ApplyBrightness(RgbaColor color, float brightness)
    {
        var clamped = Clamp01(brightness);
        return new RgbaColor(
            ClampToByte(color.R * clamped),
            ClampToByte(color.G * clamped),
            ClampToByte(color.B * clamped),
            color.A);
    }

    private static float Clamp01(float value)
    {
        if (value <= 0f)
        {
            return 0f;
        }

        return value >= 1f ? 1f : value;
    }

    private static byte ClampToByte(float value)
    {
        if (value <= 0f)
        {
            return 0;
        }

        return value >= 255f ? (byte)255 : (byte)MathF.Round(value);
    }

    private static byte ToByte01(float value) => ClampToByte(Clamp01(value) * 255f);
}
