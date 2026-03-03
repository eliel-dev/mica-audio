using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

public sealed class SimulatorLedOutput : ILedOutput
{
    private readonly object gate = new();

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
        }
    }

    public void Stop()
    {
    }

    public void Send(LedPayload payload)
    {
        lock (gate)
        {
            if (payload.Frame128x64 is { Length: > 0 })
            {
                Array.Fill(frame, new RgbaColor(0, 0, 0, 255));
                var length = Math.Min(frame.Length, payload.Frame128x64.Length);
                for (var i = 0; i < length; i++)
                {
                    frame[i] = ApplyBrightness(payload.Frame128x64[i]);
                }
            }
            else if (payload.Bins128 is { Length: LedDefaults.MatrixWidth } bins)
            {
                RenderBins(bins, payload.Level);
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

    private void RenderBins(float[] bins, float level)
    {
        Array.Fill(frame, new RgbaColor(0, 0, 0, 255));

        var h = config.Height;
        var centerTop = (h - 1) / 2;
        var centerBottom = h / 2;
        var halfHeight = Math.Max(1, h / 2);
        var maxX = Math.Min(config.Width, bins.Length);

        for (var x = 0; x < maxX; x++)
        {
            var value = Math.Clamp(bins[x], 0f, 1f);
            var litHalf = (int)MathF.Round(value * halfHeight);
            if (litHalf <= 0)
            {
                continue;
            }

            for (var offset = 0; offset < litHalf; offset++)
            {
                var t = x / (float)Math.Max(1, maxX - 1);
                var r = (byte)Math.Clamp(65f + (170f * t), 0f, 255f);
                var g = (byte)Math.Clamp(150f + (95f * (1f - MathF.Abs((2f * t) - 1f))), 0f, 255f);
                var b = (byte)Math.Clamp(45f + (170f * (1f - t)) + (level * 28f), 0f, 255f);
                var color = ApplyBrightness(new RgbaColor(r, g, b, 255));

                var yTop = centerTop - offset;
                if (yTop >= 0)
                {
                    frame[(yTop * config.Width) + x] = color;
                }

                var yBottom = centerBottom + offset;
                if (yBottom < h)
                {
                    frame[(yBottom * config.Width) + x] = color;
                }
            }
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
