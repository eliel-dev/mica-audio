using System.Numerics;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#vizzy-orbit-rings
public sealed class VizzyOrbitRingsRenderer : IRenderer
{
    private const float TwoPi = MathF.PI * 2f;

    private Vector2[] points = Array.Empty<Vector2>();
    private float phase;

    public string RendererId => RendererIds.VizzyOrbitRings;

    public string DisplayName => "Vizzy Orbit Rings";

    public void Render(RenderContext context)
    {
        var bands = context.Frame.BandsDisplay;
        if (context.Width <= 1f || context.Height <= 1f || bands.Length == 0)
        {
            return;
        }

        var ringCount = ClampInt((int)MathF.Round(context.Param("orbitRingCount", 4f)), 2, 6);
        var pointCount = ClampInt((int)MathF.Round(context.Param("orbitPointCount", 96f)), 48, 128);
        var glowPasses = ClampInt((int)MathF.Round(context.Param("orbitGlowPasses", 2f)), 1, 4);
        var baseRadiusRatio = Math.Clamp(context.Param("orbitBaseRadius", 0.24f), 0.10f, 0.42f);
        var spacingRatio = Math.Clamp(context.Param("orbitRingSpacing", 0.03f), 0.005f, 0.12f);
        var audioDepth = Math.Clamp(context.Param("orbitAudioDepth", 0.07f), 0f, 0.25f);
        var lfoDepth = Math.Clamp(context.Param("orbitLfoDepth", 0.02f), 0f, 0.12f);
        var rotationSpeed = Math.Clamp(context.Param("orbitRotationSpeed", 0.35f), 0.02f, 3f);
        var lineThickness = Math.Clamp(context.Param("orbitLineThickness", 2.2f), 0.75f, 10f);
        var coreCircleAlpha = Math.Clamp(context.Param("orbitCoreCircleAlpha", 0.90f), 0.1f, 1f);

        phase += context.DeltaSeconds * rotationSpeed;

        EnsurePointBuffer(pointCount);

        var ds = context.DrawingSession;
        var center = new Vector2(context.Width * 0.5f, context.Height * 0.5f);
        var minDimension = MathF.Min(context.Width, context.Height);
        var baseRadius = minDimension * baseRadiusRatio;
        var spacing = minDimension * spacingRatio;

        for (var ringIndex = 0; ringIndex < ringCount; ringIndex++)
        {
            var ringRadius = baseRadius + (spacing * ringIndex);
            var ringPhase = (phase * (1f + (ringIndex * 0.18f))) + (ringIndex * 0.9f);
            var ringT = ringIndex / (float)Math.Max(1, ringCount - 1);

            for (var i = 0; i < pointCount; i++)
            {
                var t = i / (float)pointCount;
                var angle = (TwoPi * t) + ringPhase;

                var bandIndex = (int)((t * bands.Length) + (ringIndex * 11)) % bands.Length;
                var nextBandIndex = (bandIndex + 1) % bands.Length;
                var audioSample = (Math.Clamp(bands[bandIndex], 0f, 1f) * 0.7f)
                    + (Math.Clamp(bands[nextBandIndex], 0f, 1f) * 0.3f);

                var lfo = MathF.Sin((angle * 2f) + phase + (ringIndex * 0.6f));
                var radiusScale = 1f + (audioDepth * audioSample) + (lfoDepth * lfo);
                var radius = MathF.Max(minDimension * 0.02f, ringRadius * radiusScale);

                points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }

            for (var pass = glowPasses; pass >= 1; pass--)
            {
                var passFactor = pass / (float)glowPasses;
                var alpha = 0.22f * passFactor;
                var thickness = lineThickness + (pass * 1.8f);
                DrawClosedPolyline(ds, context, ringT, thickness, alpha, pointCount);
            }

            DrawClosedPolyline(ds, context, ringT, lineThickness, 0.92f, pointCount);
        }

        var coreRadius = baseRadius * 0.76f;
        ds.DrawCircle(center, coreRadius, context.Palette.ColorAt(0.5f, coreCircleAlpha), Math.Max(1f, lineThickness * 0.95f));
        ds.DrawCircle(center, coreRadius * 1.04f, context.Palette.ColorAt(0.5f, coreCircleAlpha * 0.35f), Math.Max(0.5f, lineThickness * 0.42f));
    }

    private void DrawClosedPolyline(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, RenderContext context, float ringT, float thickness, float alpha, int pointCount)
    {
        for (var i = 0; i < pointCount; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % pointCount];
            var t = i / (float)Math.Max(1, pointCount - 1);
            var colorT = (ringT * 0.75f) + (t * 0.25f);
            ds.DrawLine(p0, p1, context.Palette.ColorAt(colorT, alpha), thickness);
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
