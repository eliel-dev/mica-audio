using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

// DOCS: docs/wiki/modules/output-led.md#atualizacao-2026-03-preview-hub75-local-fiel-ao-bins128-do-device
public sealed class SimulatorLedOutput : ILedOutput
{
    private readonly object gate = new();
    private readonly Bins128PreviewRenderer binsPreviewRenderer = new();

    private LedOutputConfig config = new();
    private RgbaColor[] frame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
    private float brightness = LedDefaults.Brightness;

    public bool IsAvailable => true;

    public void Start(LedOutputConfig config)
    {
        lock (gate)
        {
            this.config = config;
            frame = new RgbaColor[config.Width * config.Height];
            brightness = Math.Clamp(config.Brightness, 0f, 1f);
            binsPreviewRenderer.Reset();
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            binsPreviewRenderer.Reset();
        }
    }

    public void Send(LedPayload payload)
    {
        lock (gate)
        {
            if (payload.Frame128x64 is { Length: > 0 })
            {
                binsPreviewRenderer.Reset();
                Array.Fill(frame, new RgbaColor(0, 0, 0, 255));
                var length = Math.Min(frame.Length, payload.Frame128x64.Length);
                for (var i = 0; i < length; i++)
                {
                    frame[i] = ApplyBrightness(payload.Frame128x64[i]);
                }
            }
            else if (payload.Bins128 is { Length: LedDefaults.MatrixWidth } bins)
            {
                binsPreviewRenderer.Render(
                    bins,
                    payload.Level,
                    payload.BinsFlags,
                    config.Width,
                    config.Height,
                    brightness,
                    frame);
            }
            else
            {
                return;
            }
        }
    }

    public void SetBrightness(float value)
    {
        lock (gate)
        {
            brightness = Math.Clamp(value, 0f, 1f);
        }
    }

    public RgbaColor[] GetFrameSnapshot()
    {
        lock (gate)
        {
            var snapshot = new RgbaColor[frame.Length];
            Array.Copy(frame, snapshot, frame.Length);
            return snapshot;
        }
    }

    private RgbaColor ApplyBrightness(RgbaColor color)
    {
        var r = (byte)Math.Clamp(color.R * brightness, 0f, 255f);
        var g = (byte)Math.Clamp(color.G * brightness, 0f, 255f);
        var b = (byte)Math.Clamp(color.B * brightness, 0f, 255f);
        return new RgbaColor(r, g, b, color.A);
    }
}
