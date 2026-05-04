using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Visual.Win2D.Engine;
using Windows.UI;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#polar-arcs
public sealed class PolarArcsRenderer : IRenderer, IRendererCapabilitiesProvider
{
    private const float LeftCenterAngle = MathF.PI;
    private const float RightCenterAngle = 0f;
    private static readonly Color BarBaseColor = Color.FromArgb(255, 255, 255, 255);

    private static readonly RendererCapabilities ExplicitCapabilities = new()
    {
        UsesAnalyzerPipeline = true,
        Controls = new RendererControlSupport
        {
            SupportsBarCount = true,
        },
        BarCountMode = RendererBarCountMode.Resampled,
        IntegrationMode = RendererIntegrationMode.Explicit,
    };

    private float[] reactiveBands = Array.Empty<float>();
    private float elapsedSeconds;

    public string RendererId => RendererIds.PolarArcs;

    public string DisplayName => "Polar Arcs";

    public RendererCapabilities Capabilities => ExplicitCapabilities;

    public void Render(RenderContext context)
    {
        if (context.Width <= 1f || context.Height <= 1f)
        {
            return;
        }

        elapsedSeconds += Math.Clamp(context.DeltaSeconds, 0.001f, 0.1f);

        var requestedPairs = Math.Clamp((int)MathF.Round(Math.Max(0, context.Frame.BandsDisplay.Length) / 2f), 6, 32);
        var snapshot = ReactiveBandSampler.Sample(
            context.Frame.BandsDisplay,
            context.Frame.Level,
            context.DeltaSeconds,
            requestedPairs,
            RendererBarCountMode.Resampled,
            ref reactiveBands);

        var bands = snapshot.Bands;
        if (bands.Length == 0)
        {
            return;
        }

        var ds = context.DrawingSession;
        var center = new Vector2(context.Width * 0.5f, context.Height * 0.5f);
        var minDimension = MathF.Min(context.Width, context.Height);

        var outerRadiusFactor = Math.Clamp(context.Param("polarArcsOuterRadius", 1.00f), 0.85f, 1.15f);
        var innerGapFactor = Math.Clamp(context.Param("polarArcsInnerHoleRadius", 0.18f), 0.04f, 0.30f);
        var barsStartFactor = Math.Clamp(context.Param("polarArcsBarsStart", 0.28f), 0.08f, 0.70f);
        var barsEndFactor = Math.Clamp(context.Param("polarArcsBarsEnd", 0.92f), barsStartFactor + 0.05f, 0.97f);
        var maxSweepDegrees = Math.Clamp(context.Param("polarArcsMaxSweepDegrees", 34f), 8f, 75f);
        var jitter = Math.Clamp(context.Param("polarArcsJitter", 0.01f), 0f, 0.08f);
        var strokeFactor = Math.Clamp(context.Param("polarArcsBandThicknessFactor", 0.14f), 0.08f, 0.30f);

        var outerRadius = minDimension * 0.42f * outerRadiusFactor;
        var innerGapRadius = outerRadius * innerGapFactor;
        var barsStartRadius = Math.Max(innerGapRadius + (outerRadius * 0.04f), outerRadius * barsStartFactor);
        var barsEndRadius = Math.Min(outerRadius, outerRadius * barsEndFactor);
        var radialStep = (barsEndRadius - barsStartRadius) / Math.Max(1, bands.Length - 1);
        var strokeThickness = MathF.Max(1.35f, radialStep * strokeFactor);

        var maxHalfSweep = DegreesToRadians(maxSweepDegrees);
        var minVisibleHalfSweep = DegreesToRadians(0.9f);
        var jitterAmplitude = maxHalfSweep * jitter;

        for (var i = 0; i < bands.Length; i++)
        {
            var radius = barsStartRadius + (radialStep * i);
            var rawValue = Math.Clamp(bands[i], 0f, 1f);
            var reactiveValue = Math.Clamp(
                MathF.Pow(rawValue, 0.82f) * (0.96f + (snapshot.GlobalLevel * 0.18f)),
                0f,
                1f);
            var halfSweep = maxHalfSweep * reactiveValue;
            if (halfSweep < minVisibleHalfSweep)
            {
                continue;
            }

            var radiusBias = i / (float)Math.Max(1, bands.Length - 1);
            var offset = MathF.Sin((elapsedSeconds * 1.15f) + (i * 0.65f))
                * jitterAmplitude
                * reactiveValue
                * (0.08f + (snapshot.Mid * 0.22f));
            var opacity = Math.Clamp(0.22f + (reactiveValue * (0.58f + (snapshot.High * 0.20f))), 0f, 1f);
            var arcColor = WithOpacity(BarBaseColor, opacity * (0.92f + (radiusBias * 0.08f)));

            DrawOpenArcStroke(ds, center, radius, LeftCenterAngle + offset, halfSweep, strokeThickness, arcColor);
            DrawOpenArcStroke(ds, center, radius, RightCenterAngle - offset, halfSweep, strokeThickness, arcColor);
        }
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }

    private static Vector2 PointOnCircle(Vector2 center, float radius, float angle)
    {
        return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }

    private static void DrawOpenArcStroke(
        Microsoft.Graphics.Canvas.CanvasDrawingSession ds,
        Vector2 center,
        float radius,
        float centerAngle,
        float halfSweep,
        float strokeThickness,
        Color color)
    {
        if (radius <= 0f || halfSweep <= 0.001f || strokeThickness <= 0f)
        {
            return;
        }

        var startAngle = centerAngle - halfSweep;
        var endAngle = centerAngle + halfSweep;
        var startPoint = PointOnCircle(center, radius, startAngle);
        var endPoint = PointOnCircle(center, radius, endAngle);
        var isLargeArc = (halfSweep * 2f) > MathF.PI;

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(startPoint);
        pathBuilder.AddArc(
            endPoint,
            radius,
            radius,
            0f,
            CanvasSweepDirection.Clockwise,
            isLargeArc ? CanvasArcSize.Large : CanvasArcSize.Small);
        pathBuilder.EndFigure(CanvasFigureLoop.Open);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.DrawGeometry(geometry, color, strokeThickness);
    }

    private static Color WithOpacity(Color color, float opacity)
    {
        var clamped = (byte)Math.Clamp((int)MathF.Round(opacity * 255f), 0, 255);
        return Color.FromArgb(clamped, color.R, color.G, color.B);
    }
}
