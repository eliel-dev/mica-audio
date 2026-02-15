using System.Diagnostics;
using Device.Protocol.Stream;
using Device.Server.Hosting;
using MicaAudio.Core.Led;

namespace Output.Led;

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
        float[]? bins;
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
            level = payload.Level;
            localBrightness = brightness;
            localSequence = ++sequence;
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
}
