using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#vizzy-blob-neon
public sealed class VizzyBlobNeonRenderer : IRenderer
{
    private const float TwoPi = MathF.PI * 2f;

    private Vector2[] points = Array.Empty<Vector2>();
    private float lfoPhase;
    private float rotation;

    public string RendererId => RendererIds.VizzyBlobNeon;

    public string DisplayName => "Vizzy Blob Neon";

    public void Render(RenderContext context)
    {
        var bands = context.Frame.BandsDisplay;
        if (context.Width <= 1f || context.Height <= 1f || bands.Length == 0)
        {
            return;
        }

        var pointCount = ClampInt((int)MathF.Round(context.Param("blobPointCount", 96f)), 48, 128);
        var glowPasses = ClampInt((int)MathF.Round(context.Param("blobGlowPasses", 3f)), 1, 4);
        var baseRadiusRatio = Math.Clamp(context.Param("blobBaseRadius", 0.28f), 0.12f, 0.45f);
        var audioDepth = Math.Clamp(context.Param("blobAudioDepth", 0.11f), 0f, 0.35f);
        var lfoDepth = Math.Clamp(context.Param("blobLfoDepth", 0.03f), 0f, 0.15f);
        var lfoSpeed = Math.Clamp(context.Param("blobLfoSpeed", 1.20f), 0.05f, 4f);
        var rotationSpeed = Math.Clamp(context.Param("blobRotationSpeed", 0.18f), -3f, 3f);
        var strokeWidth = Math.Clamp(context.Param("blobStrokeWidth", 2f), 0.75f, 10f);
        var coreAlpha = Math.Clamp(context.Param("blobCoreAlpha", 0.92f), 0.1f, 1f);
        var glowAlpha = Math.Clamp(context.Param("blobGlowAlpha", 0.24f), 0.02f, 0.70f);

        lfoPhase += context.DeltaSeconds * lfoSpeed;
        rotation += context.DeltaSeconds * rotationSpeed;

        EnsurePointBuffer(pointCount);

        var center = new Vector2(context.Width * 0.5f, context.Height * 0.5f);
        var minDimension = MathF.Min(context.Width, context.Height);
        var baseRadius = minDimension * baseRadiusRatio;

        var lowBandCount = Math.Max(1, bands.Length / 4);
        var lowEnergyAccumulator = 0f;
        for (var i = 0; i < lowBandCount; i++)
        {
            lowEnergyAccumulator += Math.Clamp(bands[i], 0f, 1f);
        }

        var lowEnergy = lowEnergyAccumulator / lowBandCount;
        var blendedEnergy = Math.Clamp((context.Frame.Level * 0.6f) + (lowEnergy * 0.4f), 0f, 1f);

        for (var i = 0; i < pointCount; i++)
        {
            var t = i / (float)pointCount;
            var angle = (TwoPi * t) + rotation;

            var bandIndex = Math.Min(bands.Length - 1, (int)(t * bands.Length));
            var nextBandIndex = (bandIndex + 1) % bands.Length;
            var audioSample = (Math.Clamp(bands[bandIndex], 0f, 1f) * 0.65f)
                + (Math.Clamp(bands[nextBandIndex], 0f, 1f) * 0.35f);

            var lfoPrimary = MathF.Sin((angle * 3.0f) + lfoPhase);
            var lfoSecondary = MathF.Sin((angle * 5.0f) - (lfoPhase * 0.7f)) * 0.5f;
            var radiusScale = 1f
                + (blendedEnergy * 0.06f)
                + (audioDepth * audioSample)
                + (lfoDepth * lfoPrimary)
                + (lfoDepth * 0.4f * lfoSecondary);

            var radius = MathF.Max(minDimension * 0.02f, baseRadius * radiusScale);
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        var ds = context.DrawingSession;

        using (var pathBuilder = new CanvasPathBuilder(ds))
        {
            pathBuilder.BeginFigure(points[0].X, points[0].Y);
            for (var i = 1; i < pointCount; i++)
            {
                pathBuilder.AddLine(points[i].X, points[i].Y);
            }

            pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            using var fillGeometry = CanvasGeometry.CreatePath(pathBuilder);
            ds.FillGeometry(fillGeometry, context.Palette.ColorAt(0.55f, coreAlpha));
        }

        for (var pass = glowPasses; pass >= 1; pass--)
        {
            var passFactor = pass / (float)glowPasses;
            var alpha = glowAlpha * passFactor;
            var thickness = strokeWidth + (pass * 2.2f);

            for (var i = 0; i < pointCount; i++)
            {
                var p0 = points[i];
                var p1 = points[(i + 1) % pointCount];
                var t = i / (float)Math.Max(1, pointCount - 1);
                ds.DrawLine(p0, p1, context.Palette.ColorAt(t, alpha), thickness);
            }
        }

        for (var i = 0; i < pointCount; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % pointCount];
            var t = i / (float)Math.Max(1, pointCount - 1);
            ds.DrawLine(p0, p1, context.Palette.ColorAt(t, 0.96f), strokeWidth);
        }
    }

    private void EnsurePointBuffer(int pointCount)
    {
        if (points.Length != pointCount)
        {
            points = new Vector2[pointCount];
        }
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
