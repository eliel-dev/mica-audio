using System.Diagnostics;
using Device.Protocol.Stream;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

// DOCS: docs/wiki/modules/output-led.md#modulo-output-led
public sealed class MatrixPortalLedOutput : ILedOutput
{
    private readonly IDeviceServerHost deviceServerHost;
    private readonly object gate = new();

    private float brightness = LedDefaults.Brightness;
    private bool started;
    private uint sequence;

    public MatrixPortalLedOutput(IDeviceServerHost deviceServerHost)
    {
        this.deviceServerHost = deviceServerHost;
    }

    public bool IsAvailable => true;

    public void Start(LedOutputConfig config)
    {
        lock (gate)
        {
            started = true;
            brightness = Math.Clamp(config.Brightness, 0f, 1f);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            started = false;
        }
    }

    public void Send(LedPayload payload)
    {
        // DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
        float[]? bins;
        RgbaColor[]? frame;
        float level;
        float localBrightness;
        uint localSequence;

        lock (gate)
        {
            if (!started)
            {
                return;
            }

            bins = payload.Bins64;
            frame = payload.Frame64x32;
            level = payload.Level;
            localBrightness = brightness;
            localSequence = ++sequence;
        }

        if (frame is { Length: StreamFrameV1.PixelCount64x32 })
        {
            Span<ushort> pixels = stackalloc ushort[StreamFrameV1.PixelCount64x32];
            for (var i = 0; i < frame.Length; i++)
            {
                pixels[i] = ToRgb565(frame[i]);
            }

            var frameBytes = StreamFrameV1.CreateFrame64x32Rgb565(
                sequence: localSequence,
                timestampQpc: Stopwatch.GetTimestamp(),
                pixels64x32Rgb565: pixels,
                brightness0To255: ToByte01(localBrightness));

            deviceServerHost.BroadcastFrame(frameBytes);
            return;
        }

        if (bins is null || bins.Length != 64)
        {
            return;
        }

        Span<byte> binsBytes = stackalloc byte[64];
        for (var i = 0; i < 64; i++)
        {
            binsBytes[i] = ToByte01(bins[i]);
        }

        var bytes = StreamFrameV1.Create(
            sequence: localSequence,
            timestampQpc: Stopwatch.GetTimestamp(),
            level0To255: ToByte01(level),
            bins64: binsBytes,
            brightness0To255: ToByte01(localBrightness));

        deviceServerHost.BroadcastFrame(bytes);
    }

    public void SetBrightness(float value)
    {
        lock (gate)
        {
            brightness = Math.Clamp(value, 0f, 1f);
        }
    }

    private static byte ToByte01(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f), 0, 255);
    }

    private static ushort ToRgb565(RgbaColor color)
    {
        var r = (ushort)((color.R >> 3) & 0x1F);
        var g = (ushort)((color.G >> 2) & 0x3F);
        var b = (ushort)((color.B >> 3) & 0x1F);
        return (ushort)((r << 11) | (g << 5) | b);
    }
}


