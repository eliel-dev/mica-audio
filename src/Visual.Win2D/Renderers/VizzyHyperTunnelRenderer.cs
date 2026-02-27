using System.Numerics;
using Microsoft.Graphics.Canvas;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#vizzy-hyper-tunnel
public sealed class VizzyHyperTunnelRenderer : IRenderer
{
    private const float TwoPi = MathF.PI * 2f;

    private Vector2[] ringPoints = Array.Empty<Vector2>();
    private float animationTime;
    private float smoothedFrameMs = 16.7f;
    private float qualityCooldownSeconds;
    private int qualityTier = 2; // 2=high, 1=medium, 0=low

    public string RendererId => RendererIds.VizzyHyperTunnel;

    public string DisplayName => "Vizzy Hyper Tunnel";

    public void Render(RenderContext context)
    {
        var bands = context.Frame.BandsDisplay;
        if (context.Width <= 1f || context.Height <= 1f || bands.Length == 0)
        {
            return;
        }

        var baseSliceCount = ClampInt((int)MathF.Round(context.Param("tunnelSliceCount", 72f)), 28, 96);
        var baseSegmentCount = ClampInt((int)MathF.Round(context.Param("tunnelSegmentCount", 84f)), 36, 120);
        var baseGlowPasses = ClampInt((int)MathF.Round(context.Param("tunnelGlowPasses", 3f)), 1, 4);

        UpdateAdaptiveQuality(context.DeltaSeconds);
        var (sliceCount, segmentCount, glowPasses) = ResolveQuality(baseSliceCount, baseSegmentCount, baseGlowPasses);

        var baseRadius = Math.Clamp(context.Param("tunnelBaseRadius", 0.22f), 0.10f, 0.42f);
        var tunnelDepth = Math.Clamp(context.Param("tunnelDepth", 1.00f), 0.55f, 1.80f);
        var tunnelSpeed = Math.Clamp(context.Param("tunnelSpeed", 1.10f), 0.25f, 3.40f);
        var tunnelWarp = Math.Clamp(context.Param("tunnelWarp", 0.14f), 0.01f, 0.42f);
        var tunnelTwist = Math.Clamp(context.Param("tunnelTwist", 0.35f), 0.05f, 1.80f);
        var lineThickness = Math.Clamp(context.Param("tunnelLineThickness", 2.0f), 0.75f, 8.0f);
        var fogAmount = Math.Clamp(context.Param("tunnelFogAmount", 0.18f), 0f, 0.65f);

        EnsurePointBuffer(segmentCount + 1);

        var bass = ComputeBandEnergy(bands, 0f, 0.20f);
        var mid = ComputeBandEnergy(bands, 0.20f, 0.65f);
        var high = ComputeBandEnergy(bands, 0.65f, 1f);

        var depthPush = Math.Clamp((bass * 0.75f) + (context.Frame.Level * 0.35f), 0f, 1.4f);
        var audioDrive = Math.Clamp((bass * 0.45f) + (mid * 0.35f) + (high * 0.20f) + (context.Frame.Level * 0.25f), 0f, 1.5f);

        var speedFactor = tunnelSpeed * (0.75f + (bass * 0.85f) + (context.Frame.Level * 0.30f));
        animationTime += context.DeltaSeconds * speedFactor;

        var ds = context.DrawingSession;
        var minDimension = MathF.Min(context.Width, context.Height);
        var screenCenter = new Vector2(context.Width * 0.5f, context.Height * 0.5f);

        for (var slice = sliceCount - 1; slice >= 0; slice--)
        {
            var depthNorm = slice / (float)Math.Max(1, sliceCount - 1); // 1=far, 0=near
            var nearWeight = 1f - depthNorm;

            var distanceFactor = 0.42f + (depthNorm * (0.95f + (tunnelDepth * 0.90f)));
            var perspective = 1f / distanceFactor;
            var ringRadiusBase = minDimension * baseRadius * perspective * (0.90f + (depthPush * 0.22f));

            var noise = ValueNoise((animationTime * 0.20f) + (depthNorm * 8.2f));
            var driftScale = minDimension * (0.05f + (depthNorm * 0.03f));
            var driftX = MathF.Sin((animationTime * 0.45f) + (depthNorm * 7.5f) + (noise * 4.0f)) * driftScale;
            var driftY = MathF.Cos((animationTime * 0.31f) - (depthNorm * 5.8f) + (noise * 2.5f)) * driftScale * 0.78f;
            var ringCenter = screenCenter + new Vector2(driftX, driftY) * tunnelWarp;

            var ringTwist = (animationTime * tunnelTwist) + (depthNorm * 5.2f);
            var ringWarp = tunnelWarp * (0.50f + (mid * 0.45f) + (high * 0.35f));

            for (var seg = 0; seg <= segmentCount; seg++)
            {
                var t = seg / (float)segmentCount;
                var angle = (t * TwoPi) + ringTwist;

                var bandIndex = Math.Min(bands.Length - 1, (int)(t * bands.Length));
                var audioSample = Math.Clamp(bands[bandIndex], 0f, 1f);

                var lfoA = MathF.Sin((angle * 3.0f) + (animationTime * 1.15f) + (depthNorm * 10f));
                var lfoB = MathF.Cos((angle * 5.0f) - (animationTime * 0.70f) + (depthNorm * 12f));

                var warpFactor = 1f
                    + (ringWarp * ((audioSample * 0.85f) + (high * 0.22f)))
                    + (ringWarp * 0.38f * lfoA)
                    + (ringWarp * 0.24f * lfoB);

                var radius = MathF.Max(minDimension * 0.01f, ringRadiusBase * warpFactor);
                ringPoints[seg] = ringCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }

            var coreAlpha = Math.Clamp(0.12f + (nearWeight * 0.52f) + (audioDrive * 0.18f), 0.08f, 0.92f);
            var baseThickness = lineThickness + (nearWeight * 0.40f);

            for (var pass = glowPasses; pass >= 1; pass--)
            {
                var passFactor = pass / (float)glowPasses;
                var alpha = coreAlpha * 0.36f * passFactor;
                var thickness = baseThickness + (pass * 1.95f);
                DrawClosedPolyline(ds, context, depthNorm, thickness, alpha, segmentCount);
            }

            DrawClosedPolyline(ds, context, depthNorm, baseThickness, coreAlpha, segmentCount);

            if (fogAmount > 0f)
            {
                var fogAlpha = fogAmount * (0.015f + (depthNorm * 0.060f));
                var fogRadius = ringRadiusBase * (0.48f + (0.30f * ValueNoise((depthNorm * 23.0f) + (animationTime * 0.7f))));
                ds.FillCircle(ringCenter, fogRadius, context.Palette.ColorAt(0.82f, fogAlpha));
            }
        }
    }

    private void DrawClosedPolyline(CanvasDrawingSession drawingSession, RenderContext context, float depthNorm, float thickness, float alpha, int segmentCount)
    {
        for (var i = 0; i < segmentCount; i++)
        {
            var p0 = ringPoints[i];
            var p1 = ringPoints[i + 1];
            var t = i / (float)Math.Max(1, segmentCount - 1);

            // Near rings trend to warm tones, far rings trend to cyan tones.
            var colorT = Math.Clamp(((1f - depthNorm) * 0.35f) + (t * 0.65f), 0f, 1f);
            drawingSession.DrawLine(p0, p1, context.Palette.ColorAt(colorT, alpha), thickness);
        }
    }

    private void UpdateAdaptiveQuality(float deltaSeconds)
    {
        var frameMs = Math.Clamp(deltaSeconds * 1000f, 8f, 80f);
        smoothedFrameMs = Lerp(smoothedFrameMs, frameMs, 0.08f);

        qualityCooldownSeconds = MathF.Max(0f, qualityCooldownSeconds - deltaSeconds);
        if (qualityCooldownSeconds > 0f)
        {
            return;
        }

        if (smoothedFrameMs > 20f && qualityTier > 0)
        {
            qualityTier--;
            qualityCooldownSeconds = 0.28f;
            return;
        }

        if (smoothedFrameMs < 15f && qualityTier < 2)
        {
            qualityTier++;
            qualityCooldownSeconds = 0.55f;
        }
    }

    private (int SliceCount, int SegmentCount, int GlowPasses) ResolveQuality(int baseSliceCount, int baseSegmentCount, int baseGlowPasses)
    {
        var sliceCount = baseSliceCount;
        var segmentCount = baseSegmentCount;
        var glowPasses = baseGlowPasses;

        switch (qualityTier)
        {
            case 1:
                sliceCount = ClampInt((int)MathF.Round(baseSliceCount * 0.76f), 28, 96);
                segmentCount = ClampInt((int)MathF.Round(baseSegmentCount * 0.80f), 36, 120);
                glowPasses = ClampInt(baseGlowPasses - 1, 1, 4);
                break;
            case 0:
                sliceCount = ClampInt((int)MathF.Round(baseSliceCount * 0.52f), 28, 96);
                segmentCount = ClampInt((int)MathF.Round(baseSegmentCount * 0.66f), 36, 120);
                glowPasses = ClampInt(baseGlowPasses - 2, 1, 4);
                break;
        }

        return (sliceCount, segmentCount, glowPasses);
    }

    private void EnsurePointBuffer(int requiredSize)
    {
        if (ringPoints.Length != requiredSize)
        {
            ringPoints = new Vector2[requiredSize];
        }
    }

    private static float ComputeBandEnergy(float[] bands, float startRatio, float endRatio)
    {
        if (bands.Length == 0)
        {
            return 0f;
        }

        var start = Math.Clamp((int)(bands.Length * startRatio), 0, bands.Length - 1);
        var endExclusive = Math.Clamp((int)MathF.Ceiling(bands.Length * endRatio), start + 1, bands.Length);

        var sum = 0f;
        var count = 0;
        for (var i = start; i < endExclusive; i++)
        {
            sum += Math.Clamp(bands[i], 0f, 1f);
            count++;
        }

        return count > 0 ? (sum / count) : 0f;
    }

    private static float ValueNoise(float x)
    {
        var i = MathF.Floor(x);
        var f = x - i;
        var u = f * f * (3f - (2f * f));
        var a = Hash11(i);
        var b = Hash11(i + 1f);
        return Lerp(a, b, u);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * t);
    }

    private static float Hash11(float value)
    {
        var s = MathF.Sin(value * 127.1f) * 43758.5453f;
        return s - MathF.Floor(s);
    }

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
