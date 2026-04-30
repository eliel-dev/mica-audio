using System.Numerics;
using Microsoft.Graphics.Canvas;
using Visual.Win2D.Engine;
using Windows.UI;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#launchpad-grid
public sealed class LaunchpadGridRenderer : IRenderer, IRendererCapabilitiesProvider
{
    private const int GridSize = 8;
    private const int PadCount = GridSize * GridSize;
    private const int ButtonCount = GridSize;

    private static readonly Color DeviceBodyColor = Color.FromArgb(255, 14, 16, 18);
    private static readonly Color DeviceInsetColor = Color.FromArgb(255, 8, 11, 15);
    private static readonly Color DeviceBorderColor = Color.FromArgb(255, 92, 102, 114);
    private static readonly Color DeviceInnerBorderColor = Color.FromArgb(255, 28, 34, 40);
    private static readonly Color PadOffColor = Color.FromArgb(255, 58, 62, 70);
    private static readonly Color PadOffStrokeColor = Color.FromArgb(255, 28, 31, 36);
    private static readonly Color ButtonOffColor = Color.FromArgb(255, 72, 76, 82);
    private static readonly Color HotWhiteColor = Color.FromArgb(255, 255, 246, 224);

    private static readonly RendererCapabilities ExplicitCapabilities = new()
    {
        UsesAnalyzerPipeline = true,
        Controls = new RendererControlSupport
        {
            SupportsSensitivity = true,
            SupportsLinearBoost = true,
            SupportsBarCount = false,
            SupportsFftSize = true,
            SupportsFftSmoothing = true,
            SupportsWeightingFilter = true,
            SupportsFrequencyScale = true,
            SupportsFrequencyRange = true,
        },
        BarCountMode = RendererBarCountMode.Fixed,
        IntegrationMode = RendererIntegrationMode.Explicit,
        HubTransportMode = RendererHubTransportMode.Bins128,
        FixedVisualElementCount = PadCount,
        UnsupportedControlsHint = "Launchpad Grid usa uma grade fixa de 64 pads.",
    };

    private float[] reactivePads = Array.Empty<float>();
    private readonly float[] padLevels = new float[PadCount];
    private readonly float[] padColorTs = new float[PadCount];
    private readonly float[] topButtonLevels = new float[ButtonCount];
    private readonly float[] topButtonColorTs = new float[ButtonCount];
    private readonly float[] sideButtonLevels = new float[ButtonCount];
    private readonly float[] sideButtonColorTs = new float[ButtonCount];

    private float elapsedSeconds;
    private float bassStepAccumulator;
    private float midStepAccumulator;
    private float highStepAccumulator;
    private float bassCooldown;
    private float midCooldown;
    private float highCooldown;
    private float sceneCooldown;
    private float lastLow;
    private float lastMid;
    private float lastHigh;
    private float lastGlobalLevel;
    private int bassCursor = 1;
    private int midCursor = 3;
    private int highCursor = 5;
    private int sceneIndex;

    public string RendererId => RendererIds.LaunchpadGrid;

    public string DisplayName => "Launchpad Grid";

    public RendererCapabilities Capabilities => ExplicitCapabilities;

    public void Render(RenderContext context)
    {
        if (context.Width <= 1f || context.Height <= 1f)
        {
            return;
        }

        var dt = Math.Clamp(context.DeltaSeconds, 0.001f, 0.1f);
        elapsedSeconds += dt;

        var snapshot = ReactiveBandSampler.Sample(
            context.Frame.BandsDisplay,
            context.Frame.Level,
            dt,
            PadCount,
            RendererBarCountMode.Fixed,
            ref reactivePads);

        DecayChannels(dt);

        var lowRise = MathF.Max(0f, snapshot.Low - lastLow);
        var midRise = MathF.Max(0f, snapshot.Mid - lastMid);
        var highRise = MathF.Max(0f, snapshot.High - lastHigh);
        var globalRise = MathF.Max(0f, snapshot.GlobalLevel - lastGlobalLevel);

        bassCooldown = MathF.Max(0f, bassCooldown - dt);
        midCooldown = MathF.Max(0f, midCooldown - dt);
        highCooldown = MathF.Max(0f, highCooldown - dt);
        sceneCooldown = MathF.Max(0f, sceneCooldown - dt);

        var pace = Math.Clamp(context.Param("launchpadDriftSpeed", 0.85f), 0.20f, 2.5f);
        bassStepAccumulator += dt * ((0.65f + (pace * 0.55f)) + (snapshot.Low * 3.8f) + (snapshot.GlobalLevel * 0.8f));
        midStepAccumulator += dt * ((0.90f + (pace * 0.75f)) + (snapshot.Mid * 4.6f) + (snapshot.GlobalLevel * 1.0f));
        highStepAccumulator += dt * ((1.40f + (pace * 1.10f)) + (snapshot.High * 6.2f) + (snapshot.GlobalLevel * 1.1f));

        var bassPulseStrength = Math.Clamp((snapshot.Low * 0.88f) + (lowRise * 4.6f) + (globalRise * 1.20f), 0f, 1.6f);
        var midPulseStrength = Math.Clamp((snapshot.Mid * 0.78f) + (midRise * 4.2f) + (globalRise * 0.90f), 0f, 1.6f);
        var highPulseStrength = Math.Clamp((snapshot.High * 0.72f) + (highRise * 4.8f) + (globalRise * 0.70f), 0f, 1.8f);
        var scenePulseStrength = Math.Clamp((snapshot.GlobalLevel * 0.70f) + (globalRise * 4.0f) + (lowRise * 1.4f), 0f, 1.8f);

        while (bassStepAccumulator >= 1f)
        {
            bassStepAccumulator -= 1f;
            AdvanceBassPattern(in snapshot, 0.26f + (snapshot.Low * 0.30f), pulse: false);
        }

        while (midStepAccumulator >= 1f)
        {
            midStepAccumulator -= 1f;
            AdvanceMidPattern(in snapshot, 0.22f + (snapshot.Mid * 0.28f), pulse: false);
        }

        while (highStepAccumulator >= 1f)
        {
            highStepAccumulator -= 1f;
            AdvanceHighPattern(in snapshot, 0.22f + (snapshot.High * 0.26f), pulse: false);
        }

        if (bassCooldown <= 0f && bassPulseStrength >= 0.78f)
        {
            bassCooldown = 0.08f;
            bassStepAccumulator = 0f;
            AdvanceBassPattern(in snapshot, 0.50f + (bassPulseStrength * 0.32f), pulse: true);
        }

        if (midCooldown <= 0f && midPulseStrength >= 0.72f)
        {
            midCooldown = 0.07f;
            midStepAccumulator = 0f;
            AdvanceMidPattern(in snapshot, 0.44f + (midPulseStrength * 0.30f), pulse: true);
        }

        if (highCooldown <= 0f && highPulseStrength >= 0.62f)
        {
            highCooldown = 0.055f;
            highStepAccumulator = 0f;
            AdvanceHighPattern(in snapshot, 0.40f + (highPulseStrength * 0.36f), pulse: true);
        }

        if (sceneCooldown <= 0f && scenePulseStrength >= 0.74f)
        {
            sceneCooldown = 0.16f;
            TriggerScenePulse(in snapshot, 0.34f + (scenePulseStrength * 0.22f));
        }

        PaintSustain(in snapshot);
        DrawLaunchpad(context, in snapshot);

        lastLow = snapshot.Low;
        lastMid = snapshot.Mid;
        lastHigh = snapshot.High;
        lastGlobalLevel = snapshot.GlobalLevel;
    }

    private void DrawLaunchpad(RenderContext context, in ReactiveBandSnapshot snapshot)
    {
        var ds = context.DrawingSession;
        var gapRatio = Math.Clamp(context.Param("launchpadGap", 0.16f), 0.08f, 0.32f);
        var cornerRatio = Math.Clamp(context.Param("launchpadCorner", 0.22f), 0.10f, 0.42f);
        var bloomAmount = Math.Clamp(context.Param("launchpadBloom", 0.55f), 0.12f, 1.10f);
        var idleLevel = Math.Clamp(context.Param("launchpadIdleLevel", 0.025f), 0f, 0.12f);
        var flashThreshold = Math.Clamp(context.Param("launchpadFlashThreshold", 0.58f), 0.40f, 0.90f);

        var deviceSize = MathF.Min(context.Height * 0.90f, context.Width * 0.54f);
        var deviceX = (context.Width - deviceSize) * 0.5f;
        var deviceY = (context.Height - deviceSize) * 0.5f;
        var deviceRadius = MathF.Max(4f, deviceSize * 0.09f);
        var outerMargin = deviceSize * 0.08f;
        var controlBand = deviceSize * 0.14f;
        var controlGap = deviceSize * 0.03f;
        var gridSize = deviceSize - (outerMargin * 2f) - controlBand - controlGap;
        var gridLeft = deviceX + outerMargin;
        var gridTop = deviceY + outerMargin + controlBand + controlGap;
        var sideButtonCenterX = gridLeft + gridSize + controlGap + (controlBand * 0.46f);
        var topButtonCenterY = deviceY + outerMargin + (controlBand * 0.46f);
        var cell = gridSize / GridSize;
        var padSize = MathF.Max(2f, cell * (1f - gapRatio));
        var padInset = MathF.Max(0f, (cell - padSize) * 0.5f);
        var padRadius = MathF.Max(1.1f, padSize * cornerRatio);
        var buttonRadius = MathF.Max(1.2f, cell * 0.23f);
        var ambientPulse = 0.5f + (0.5f * MathF.Sin(elapsedSeconds * 0.6f));
        var haloAlpha = (idleLevel * (0.25f + (ambientPulse * 0.30f))) + (snapshot.GlobalLevel * 0.08f);

        for (var pass = 3; pass >= 1; pass--)
        {
            var grow = pass * deviceSize * 0.022f;
            ds.FillRoundedRectangle(
                deviceX - grow,
                deviceY - grow,
                deviceSize + (grow * 2f),
                deviceSize + (grow * 2f),
                deviceRadius + grow,
                deviceRadius + grow,
                context.Palette.ColorAt(0.14f, haloAlpha / pass));
        }

        ds.FillRoundedRectangle(deviceX, deviceY, deviceSize, deviceSize, deviceRadius, deviceRadius, DeviceBodyColor);
        ds.DrawRoundedRectangle(deviceX, deviceY, deviceSize, deviceSize, deviceRadius, deviceRadius, DeviceBorderColor, MathF.Max(1f, deviceSize * 0.018f));

        var inset = deviceSize * 0.04f;
        ds.FillRoundedRectangle(
            deviceX + inset,
            deviceY + inset,
            deviceSize - (inset * 2f),
            deviceSize - (inset * 2f),
            MathF.Max(3f, deviceRadius - inset),
            MathF.Max(3f, deviceRadius - inset),
            DeviceInsetColor);
        ds.DrawRoundedRectangle(
            deviceX + inset,
            deviceY + inset,
            deviceSize - (inset * 2f),
            deviceSize - (inset * 2f),
            MathF.Max(3f, deviceRadius - inset),
            MathF.Max(3f, deviceRadius - inset),
            DeviceInnerBorderColor,
            1f);

        for (var col = 0; col < GridSize; col++)
        {
            var buttonCenterX = gridLeft + (col * cell) + (cell * 0.5f);
            DrawButton(
                ds,
                new Vector2(buttonCenterX, topButtonCenterY),
                buttonRadius,
                topButtonLevels[col],
                topButtonColorTs[col],
                context.Palette,
                bloomAmount,
                warmBias: true);
        }

        for (var row = 0; row < GridSize; row++)
        {
            var buttonCenterY = gridTop + (row * cell) + (cell * 0.5f);
            DrawButton(
                ds,
                new Vector2(sideButtonCenterX, buttonCenterY),
                buttonRadius,
                sideButtonLevels[row],
                sideButtonColorTs[row],
                context.Palette,
                bloomAmount,
                warmBias: row >= 5);
        }

        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                var index = (row * GridSize) + col;
                var x = gridLeft + (col * cell) + padInset;
                var y = gridTop + (row * cell) + padInset;
                DrawPad(
                    ds,
                    x,
                    y,
                    padSize,
                    padRadius,
                    padLevels[index],
                    padColorTs[index],
                    context.Palette,
                    bloomAmount,
                    flashThreshold);
            }
        }
    }

    private void AdvanceBassPattern(in ReactiveBandSnapshot snapshot, float strength, bool pulse)
    {
        bassCursor = (bassCursor + 1) % GridSize;
        var intensity = Math.Clamp(strength, 0.16f, 1f);

        switch (sceneIndex)
        {
            case 0:
                LightHorizontalBand(7, bassCursor, pulse ? 3 : 2, intensity, 0.18f);
                if (pulse || snapshot.Low > 0.56f)
                {
                    LightHorizontalBand(6, bassCursor, 2, intensity * 0.84f, 0.14f);
                }

                break;
            case 1:
                LightBlock(6, bassCursor - 1, 2, pulse ? 3 : 2, intensity, 0.20f);
                LightHorizontalBand(5, bassCursor, 2, intensity * 0.66f, 0.24f);
                break;
            case 2:
                LightVerticalStroke(5, 7, bassCursor, intensity, 0.16f);
                LightHorizontalBand(7, bassCursor, 2, intensity * 0.82f, 0.12f);
                break;
            default:
                LightHorizontalBand(7, bassCursor, 2, intensity, 0.18f);
                LightHorizontalBand(7, GridSize - 1 - bassCursor, 2, intensity * 0.72f, 0.22f);
                if (pulse)
                {
                    LightHorizontalBand(6, bassCursor, 2, intensity * 0.74f, 0.14f);
                }

                break;
        }

        ActivateSideButton(7, intensity * 0.92f, 0.16f);
        if (pulse)
        {
            ActivateSideButton(6, intensity * 0.76f, 0.20f);
        }
    }

    private void AdvanceMidPattern(in ReactiveBandSnapshot snapshot, float strength, bool pulse)
    {
        midCursor = (midCursor + 1 + (pulse ? 1 : 0)) % GridSize;
        var row = 3 + ((midCursor + sceneIndex) % 3);
        var intensity = Math.Clamp(strength, 0.14f, 1f);

        LightVerticalStroke(2, 5, midCursor, intensity, 0.58f);
        LightHorizontalBand(row, midCursor, pulse ? 3 : 2, intensity * 0.88f, 0.48f);

        if (pulse || snapshot.Mid > 0.52f)
        {
            LightBlock(row - 1, midCursor - 1, 2, 2, intensity * 0.68f, 0.64f);
        }

        if ((sceneIndex & 1) == 1)
        {
            LightVerticalStroke(3, 5, GridSize - 1 - midCursor, intensity * 0.54f, 0.70f);
        }

        ActivateTopButton(midCursor, intensity * 0.52f, 0.56f);
    }

    private void AdvanceHighPattern(in ReactiveBandSnapshot snapshot, float strength, bool pulse)
    {
        highCursor = (highCursor + 1) % GridSize;
        var anchorRow = (sceneIndex + highCursor) % 3;
        var intensity = Math.Clamp(strength, 0.12f, 1f);

        ActivateTopButton(highCursor, intensity, 0.94f);
        ActivateSideButton(anchorRow + 1, intensity * 0.82f, 0.92f);

        ActivatePad(anchorRow, highCursor, intensity, 0.96f);
        ActivatePad(Math.Min(2, anchorRow + 1), ClampCell(highCursor - 1 + (sceneIndex % 3)), intensity * 0.72f, 0.88f);

        if (pulse || snapshot.High > 0.48f)
        {
            ActivatePad(anchorRow, ClampCell(highCursor + 1), intensity * 0.64f, 0.98f);
            ActivatePad(Math.Min(2, anchorRow + 1), highCursor, intensity * 0.58f, 0.90f);
        }
    }

    private void TriggerScenePulse(in ReactiveBandSnapshot snapshot, float intensity)
    {
        sceneIndex = (sceneIndex + 1) % 4;
        var clamped = Math.Clamp(intensity, 0.18f, 1f);

        for (var col = 0; col < GridSize; col++)
        {
            ActivateTopButton(col, clamped * (((col + sceneIndex) & 1) == 0 ? 0.70f : 0.44f), 0.18f);
        }

        for (var row = 0; row < GridSize; row++)
        {
            ActivateSideButton(row, clamped * (((row + sceneIndex) & 1) == 0 ? 0.64f : 0.38f), row >= 5 ? 0.18f : 0.90f);
        }

        var perimeterColor = snapshot.Low >= snapshot.High ? 0.18f : 0.94f;
        for (var i = 0; i < GridSize; i++)
        {
            ActivatePad(0, i, clamped * 0.26f, perimeterColor);
            ActivatePad(7, i, clamped * 0.30f, 0.16f);
            ActivatePad(i, 0, clamped * 0.22f, perimeterColor);
            ActivatePad(i, 7, clamped * 0.24f, perimeterColor);
        }
    }

    private void PaintSustain(in ReactiveBandSnapshot snapshot)
    {
        var bassSustain = 0.02f + (snapshot.Low * 0.28f);
        var midSustain = 0.02f + (snapshot.Mid * 0.24f);
        var highSustain = 0.02f + (snapshot.High * 0.20f);

        LightHorizontalBand(7, bassCursor, 2, bassSustain, 0.16f);
        if (snapshot.Low > 0.40f)
        {
            LightHorizontalBand(6, bassCursor, 2, bassSustain * 0.70f, 0.18f);
        }

        ActivateSideButton(7, 0.04f + (snapshot.Low * 0.22f), 0.18f);

        LightVerticalStroke(3, 5, midCursor, midSustain, 0.56f);
        ActivatePad(3 + (sceneIndex % 3), midCursor, midSustain * 0.90f, 0.50f);

        ActivateTopButton(highCursor, 0.04f + (snapshot.High * 0.22f), 0.94f);
        ActivateSideButton(1 + (sceneIndex % 3), 0.04f + (snapshot.High * 0.16f), 0.92f);
        ActivatePad(sceneIndex % 3, highCursor, highSustain * 0.92f, 0.92f);
    }

    private static void DrawPad(
        CanvasDrawingSession drawingSession,
        float x,
        float y,
        float size,
        float radius,
        float level,
        float colorT,
        PaletteSampler palette,
        float bloomAmount,
        float flashThreshold)
    {
        drawingSession.FillRoundedRectangle(x, y, size, size, radius, radius, PadOffColor);
        drawingSession.DrawRoundedRectangle(x, y, size, size, radius, radius, PadOffStrokeColor, 1f);

        if (level <= 0.01f)
        {
            return;
        }

        var litColor = WithOpacity(palette.ColorAt(colorT), 1f);
        var glowAlpha = Math.Clamp((0.12f + (level * 0.34f)) * bloomAmount, 0f, 0.85f);
        for (var pass = 3; pass >= 1; pass--)
        {
            var grow = pass * MathF.Max(0.55f, size * 0.08f);
            drawingSession.FillRoundedRectangle(
                x - grow,
                y - grow,
                size + (grow * 2f),
                size + (grow * 2f),
                radius + grow,
                radius + grow,
                WithOpacity(litColor, glowAlpha / (pass + 0.35f)));
        }

        var fillInset = Lerp(size * 0.28f, size * 0.10f, level);
        var fillSize = MathF.Max(1f, size - (fillInset * 2f));
        var fillRadius = MathF.Max(0.75f, MathF.Min(radius, fillSize * 0.5f));
        drawingSession.FillRoundedRectangle(
            x + fillInset,
            y + fillInset,
            fillSize,
            fillSize,
            fillRadius,
            fillRadius,
            WithOpacity(litColor, 0.22f + (level * 0.78f)));

        var coreInset = Lerp(size * 0.36f, size * 0.19f, level);
        var coreSize = MathF.Max(0.75f, size - (coreInset * 2f));
        var coreColor = MixColors(litColor, HotWhiteColor, 0.25f + (level * 0.32f));
        drawingSession.FillRoundedRectangle(
            x + coreInset,
            y + coreInset,
            coreSize,
            coreSize,
            MathF.Max(0.75f, MathF.Min(radius, coreSize * 0.5f)),
            MathF.Max(0.75f, MathF.Min(radius, coreSize * 0.5f)),
            WithOpacity(coreColor, 0.14f + (level * 0.44f)));

        if (level >= flashThreshold)
        {
            var flash = Math.Clamp((level - flashThreshold) / Math.Max(0.01f, 1f - flashThreshold), 0f, 1f);
            var flashInset = size * Lerp(0.26f, 0.18f, flash);
            var flashSize = MathF.Max(0.75f, size - (flashInset * 2f));
            var flashColor = MixColors(coreColor, HotWhiteColor, 0.55f + (flash * 0.25f));
            drawingSession.FillRoundedRectangle(
                x + flashInset,
                y + flashInset,
                flashSize,
                flashSize,
                MathF.Max(0.75f, MathF.Min(radius, flashSize * 0.5f)),
                MathF.Max(0.75f, MathF.Min(radius, flashSize * 0.5f)),
                WithOpacity(flashColor, 0.18f + (flash * 0.58f)));
        }
    }

    private static void DrawButton(
        CanvasDrawingSession drawingSession,
        Vector2 center,
        float radius,
        float level,
        float colorT,
        PaletteSampler palette,
        float bloomAmount,
        bool warmBias)
    {
        drawingSession.FillCircle(center, radius, ButtonOffColor);

        if (level <= 0.01f)
        {
            return;
        }

        var baseColor = palette.ColorAt(colorT);
        var litColor = warmBias
            ? MixColors(baseColor, HotWhiteColor, 0.18f + (level * 0.22f))
            : baseColor;

        for (var pass = 3; pass >= 1; pass--)
        {
            var grow = pass * MathF.Max(0.40f, radius * 0.28f);
            drawingSession.FillCircle(center, radius + grow, WithOpacity(litColor, ((0.08f + (level * 0.22f)) * bloomAmount) / (pass + 0.2f)));
        }

        drawingSession.FillCircle(center, radius * 0.90f, WithOpacity(litColor, 0.26f + (level * 0.74f)));
        drawingSession.FillCircle(center, radius * 0.42f, WithOpacity(HotWhiteColor, 0.12f + (level * 0.40f)));
    }

    private void ActivatePad(int row, int col, float intensity, float colorT)
    {
        if ((uint)row >= GridSize || (uint)col >= GridSize)
        {
            return;
        }

        var index = (row * GridSize) + col;
        padLevels[index] = Math.Max(padLevels[index], Math.Clamp(intensity, 0f, 1f));
        padColorTs[index] = colorT;
    }

    private void ActivateTopButton(int index, float intensity, float colorT)
    {
        if ((uint)index >= ButtonCount)
        {
            return;
        }

        topButtonLevels[index] = Math.Max(topButtonLevels[index], Math.Clamp(intensity, 0f, 1f));
        topButtonColorTs[index] = colorT;
    }

    private void ActivateSideButton(int index, float intensity, float colorT)
    {
        if ((uint)index >= ButtonCount)
        {
            return;
        }

        sideButtonLevels[index] = Math.Max(sideButtonLevels[index], Math.Clamp(intensity, 0f, 1f));
        sideButtonColorTs[index] = colorT;
    }

    private void LightHorizontalBand(int row, int centerCol, int width, float intensity, float colorT)
    {
        var start = centerCol - (width / 2);
        for (var i = 0; i < width; i++)
        {
            ActivatePad(row, start + i, intensity * (i == width / 2 ? 1f : 0.86f), colorT);
        }
    }

    private void LightVerticalStroke(int startRow, int endRow, int col, float intensity, float colorT)
    {
        for (var row = startRow; row <= endRow; row++)
        {
            ActivatePad(row, col, intensity * (row == startRow || row == endRow ? 0.82f : 1f), colorT);
        }
    }

    private void LightBlock(int topRow, int leftCol, int rows, int cols, float intensity, float colorT)
    {
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                ActivatePad(topRow + row, leftCol + col, intensity * ((row == 0 && col == 0) ? 0.92f : 1f), colorT);
            }
        }
    }

    private void DecayChannels(float dt)
    {
        var padFactor = MathF.Exp(-dt * 7.8f);
        var buttonFactor = MathF.Exp(-dt * 9.2f);

        DecayArray(padLevels, padFactor);
        DecayArray(topButtonLevels, buttonFactor);
        DecayArray(sideButtonLevels, buttonFactor);
    }

    private static void DecayArray(float[] values, float factor)
    {
        for (var i = 0; i < values.Length; i++)
        {
            values[i] *= factor;
            if (values[i] < 0.01f)
            {
                values[i] = 0f;
            }
        }
    }

    private static int ClampCell(int value)
        => Math.Clamp(value, 0, GridSize - 1);

    private static float Lerp(float start, float end, float amount)
        => start + ((end - start) * Math.Clamp(amount, 0f, 1f));

    private static Color WithOpacity(Color color, float alpha)
        => Color.FromArgb((byte)Math.Clamp((int)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f), 0, 255), color.R, color.G, color.B);

    private static Color MixColors(Color left, Color right, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            255,
            (byte)Math.Clamp((int)MathF.Round(left.R + ((right.R - left.R) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(left.G + ((right.G - left.G) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(left.B + ((right.B - left.B) * t)), 0, 255));
    }
}
