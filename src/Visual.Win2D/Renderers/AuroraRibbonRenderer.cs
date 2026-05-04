using System.Numerics;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#aurora-ribbon
public sealed class AuroraRibbonRenderer : IRenderer, IRendererCapabilitiesProvider
{
    private const float TwoPi = MathF.PI * 2f;
    private const int RibbonCount = 3;

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

    public string RendererId => RendererIds.AuroraRibbon;

    public string DisplayName => "Aurora Ribbon";

    public RendererCapabilities Capabilities => ExplicitCapabilities;

    public void Render(RenderContext context)
    {
        if (context.Width <= 1f || context.Height <= 1f)
        {
            return;
        }

        elapsedSeconds += Math.Clamp(context.DeltaSeconds, 0.001f, 0.1f);

        var targetBandCount = ClampInt(context.Frame.BandsDisplay.Length <= 0 ? 32 : context.Frame.BandsDisplay.Length, 16, 48);
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
        var segmentCount = ClampInt((int)MathF.Round(context.Width / 4f), 48, 180);
        var baseThickness = context.Height * Math.Clamp(context.Param("auroraThickness", 0.13f), 0.06f, 0.28f);
        var waveDepth = context.Height * Math.Clamp(context.Param("auroraWaveDepth", 0.15f), 0.04f, 0.30f);
        var audioDepth = context.Height * Math.Clamp(context.Param("auroraAudioDepth", 0.22f), 0.05f, 0.38f);
        var glowAlpha = Math.Clamp(context.Param("auroraGlowAlpha", 0.24f), 0.04f, 0.70f);
        var driftSpeed = Math.Clamp(context.Param("auroraDriftSpeed", 0.70f), 0.05f, 3f);
        var bassBloom = Math.Clamp(context.Param("auroraBassBloom", 0.18f), 0.02f, 0.42f);

        for (var ribbonIndex = 0; ribbonIndex < RibbonCount; ribbonIndex++)
        {
            var ribbonT = ribbonIndex / (float)Math.Max(1, RibbonCount - 1);
            var baseline = context.Height * (0.24f + (ribbonIndex * 0.23f));
            var thickness = MathF.Max(2f, baseThickness * (1f - (ribbonIndex * 0.12f)) * (0.85f + (snapshot.GlobalLevel * 0.45f)));
            var ribbonPhase = (elapsedSeconds * driftSpeed * (1f + (ribbonIndex * 0.14f))) + (ribbonIndex * 0.9f);
            var previousPoint = new Vector2(0f, baseline);
            var hasPreviousPoint = false;

            for (var i = 0; i < segmentCount; i++)
            {
                var t = i / (float)Math.Max(1, segmentCount - 1);
                var x = t * context.Width;

                var bandIndex = Math.Min(bands.Length - 1, (int)MathF.Round(t * (bands.Length - 1)));
                var nextBandIndex = (bandIndex + 2 + ribbonIndex) % bands.Length;
                var audioSample = (Math.Clamp(bands[bandIndex], 0f, 1f) * 0.66f)
                    + (Math.Clamp(bands[nextBandIndex], 0f, 1f) * 0.34f);

                var primaryWave = MathF.Sin((t * TwoPi * (1.25f + (ribbonIndex * 0.55f))) + ribbonPhase);
                var shimmerWave = MathF.Sin((t * TwoPi * (3.20f + ribbonIndex)) - (ribbonPhase * 0.75f)) * 0.42f;
                var energyOffset = (audioSample - 0.5f) * audioDepth * (0.90f + (snapshot.Low * 0.30f));
                var waveOffset = primaryWave * waveDepth * (0.42f + (snapshot.Mid * 0.36f))
                    + shimmerWave * waveDepth * 0.20f;
                var y = Math.Clamp(baseline + waveOffset + energyOffset, 0f, context.Height);
                var currentPoint = new Vector2(x, y);

                if (hasPreviousPoint)
                {
                    var colorT = Wrap01((ribbonT * 0.42f) + (t * 0.28f) + (snapshot.High * 0.12f));
                    var glowScale = 0.58f + (audioSample * 0.52f);

                    for (var pass = 3; pass >= 1; pass--)
                    {
                        var passFactor = pass / 3f;
                        var alpha = glowAlpha * passFactor * glowScale;
                        ds.DrawLine(
                            previousPoint,
                            currentPoint,
                            context.Palette.ColorAt(colorT, alpha),
                            thickness + (pass * (context.Height * 0.032f)));
                    }

                    ds.DrawLine(previousPoint, currentPoint, context.Palette.ColorAt(colorT, 0.94f), thickness);
                }

                previousPoint = currentPoint;
                hasPreviousPoint = true;
            }
        }

        var bloomCenter = new Vector2(
            context.Width * (0.5f + (MathF.Sin(elapsedSeconds * 0.75f) * 0.05f * snapshot.Mid)),
            context.Height * (0.52f - ((snapshot.High - snapshot.Low) * 0.08f)));
        var bloomRadius = context.Height * (0.08f + (bassBloom * (0.55f + snapshot.Low)));
        ds.FillCircle(bloomCenter, bloomRadius * 1.55f, context.Palette.ColorAt(0.08f, 0.10f + (snapshot.Low * 0.08f)));
        ds.FillCircle(bloomCenter, bloomRadius, context.Palette.ColorAt(0.18f, 0.20f + (snapshot.Low * 0.22f)));
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
