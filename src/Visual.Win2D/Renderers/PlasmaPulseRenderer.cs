using System.Numerics;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#plasma-pulse
public sealed class PlasmaPulseRenderer : IRenderer, IRendererCapabilitiesProvider
{
    private static readonly RendererCapabilities ExplicitCapabilities = new()
    {
        UsesAnalyzerPipeline = true,
        Controls = new RendererControlSupport
        {
            SupportsBarCount = true,
        },
        BarCountMode = RendererBarCountMode.Resampled,
        IntegrationMode = RendererIntegrationMode.Explicit,
        HubTransportMode = RendererHubTransportMode.Bins128,
    };

    private float[] reactiveBands = Array.Empty<float>();
    private float elapsedSeconds;

    public string RendererId => RendererIds.PlasmaPulse;

    public string DisplayName => "Plasma Pulse";

    public RendererCapabilities Capabilities => ExplicitCapabilities;

    public void Render(RenderContext context)
    {
        if (context.Width <= 1f || context.Height <= 1f)
        {
            return;
        }

        elapsedSeconds += Math.Clamp(context.DeltaSeconds, 0.001f, 0.1f);

        var targetBandCount = ClampInt(context.Frame.BandsDisplay.Length <= 0 ? 32 : context.Frame.BandsDisplay.Length, 16, 56);
        var snapshot = ReactiveBandSampler.Sample(
            context.Frame.BandsDisplay,
            context.Frame.Level,
            context.DeltaSeconds,
            targetBandCount,
            RendererBarCountMode.Resampled,
            ref reactiveBands);

        var bands = snapshot.Bands;
        if (bands.Length == 0)
        {
            return;
        }

        var ds = context.DrawingSession;
        var cellsX = ClampInt((int)MathF.Round(context.Param("plasmaCellsX", 56f)), 24, 96);
        var cellsY = ClampInt((int)MathF.Round(cellsX * (context.Height / Math.Max(1f, context.Width))), 16, 64);
        var cellWidth = context.Width / cellsX;
        var cellHeight = context.Height / cellsY;
        var speed = Math.Clamp(context.Param("plasmaSpeed", 0.90f), 0.05f, 3f);
        var warp = Math.Clamp(context.Param("plasmaWarp", 0.72f), 0.05f, 2.5f);
        var bassPulse = Math.Clamp(context.Param("plasmaBassPulse", 0.30f), 0.05f, 0.90f);
        var contrast = Math.Clamp(context.Param("plasmaContrast", 1.35f), 0.75f, 2.4f);

        for (var y = 0; y < cellsY; y++)
        {
            var ny = cellsY == 1 ? 0f : y / (float)(cellsY - 1);
            var centeredY = (ny - 0.5f) * (1.9f + (snapshot.High * 0.18f));

            for (var x = 0; x < cellsX; x++)
            {
                var nx = cellsX == 1 ? 0f : x / (float)(cellsX - 1);
                var centeredX = (nx - 0.5f) * (1.9f + (snapshot.Low * 0.22f));
                var bandIndex = Math.Min(bands.Length - 1, (int)MathF.Round(nx * (bands.Length - 1)));
                var nextBandIndex = (bandIndex + 3) % bands.Length;
                var audioSample = (Math.Clamp(bands[bandIndex], 0f, 1f) * 0.68f)
                    + (Math.Clamp(bands[nextBandIndex], 0f, 1f) * 0.32f);

                var radial = MathF.Sqrt((centeredX * centeredX) + (centeredY * centeredY));
                var waveA = MathF.Sin((centeredX * 6.2f) + (elapsedSeconds * speed * 2.45f) + (audioSample * warp * 4.2f));
                var waveB = MathF.Sin((centeredY * 8.4f) - (elapsedSeconds * speed * 1.7f) + (snapshot.Mid * 5.2f));
                var waveC = MathF.Sin(((centeredX + centeredY) * 7.1f) + (elapsedSeconds * speed * 1.25f) + (snapshot.High * 3.6f));
                var shockwave = MathF.Sin((radial * 18.5f) - (elapsedSeconds * speed * 4.1f) - (snapshot.Low * 2.8f));
                var plasma = (waveA + waveB + waveC + (shockwave * (0.40f + (snapshot.Low * bassPulse)))) * 0.25f;

                var intensity = Math.Clamp(
                    (plasma * 0.5f) + 0.5f + (audioSample * 0.16f) + (snapshot.GlobalLevel * 0.06f),
                    0f,
                    1f);
                intensity = MathF.Pow(intensity, contrast);
                if (intensity <= 0.03f)
                {
                    continue;
                }

                var colorT = Wrap01(0.5f + (plasma * 0.28f) + (audioSample * 0.22f) + (elapsedSeconds * 0.06f));
                var alpha = Math.Clamp(0.18f + (intensity * 0.82f), 0.12f, 1f);
                ds.FillRectangle(
                    x * cellWidth,
                    y * cellHeight,
                    MathF.Ceiling(cellWidth) + 0.75f,
                    MathF.Ceiling(cellHeight) + 0.75f,
                    context.Palette.ColorAt(colorT, alpha));
            }
        }

        var coreCenter = new Vector2(
            context.Width * (0.5f + (MathF.Sin(elapsedSeconds * 0.9f) * 0.07f * snapshot.Mid)),
            context.Height * 0.5f);
        var coreRadius = context.Height * (0.07f + (snapshot.Low * 0.17f));
        ds.DrawCircle(coreCenter, coreRadius * 1.6f, context.Palette.ColorAt(0.14f, 0.14f + (snapshot.Low * 0.14f)), MathF.Max(1f, context.Height * 0.020f));
        ds.DrawCircle(coreCenter, coreRadius, context.Palette.ColorAt(0.74f, 0.28f + (snapshot.Mid * 0.20f)), MathF.Max(1f, context.Height * 0.016f));
    }

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static float Wrap01(float value)
    {
        var wrapped = value - MathF.Floor(value);
        return wrapped < 0f ? wrapped + 1f : wrapped;
    }
}
