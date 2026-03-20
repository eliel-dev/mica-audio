using System.Diagnostics;
using Device.Protocol.Stream;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

// DOCS: docs/wiki/modules/output-led.md#modulo-output-led
// DOCS: docs/wiki/reference/ws-protocol-v2.md#estrutura-streamframev2
public sealed class Esp32S3LedOutput : ILedOutput
{
    private readonly IDeviceServerHost deviceServerHost;
    private readonly object gate = new();

    private float brightness = LedDefaults.Brightness;
    private string? targetDeviceId;
    private bool started;
    private uint sequence;
    private ushort[]? lastFrameRgb565;
    private ushort[]? frameScratchRgb565;
    private byte lastFrameBrightness;
    private bool hasLastFrame;

    public Esp32S3LedOutput(IDeviceServerHost deviceServerHost)
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
            targetDeviceId = string.IsNullOrWhiteSpace(config.TargetDeviceId)
                ? null
                : config.TargetDeviceId.Trim();
            hasLastFrame = false;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            started = false;
            targetDeviceId = null;
            hasLastFrame = false;
        }
    }

    public void Send(LedPayload payload)
    {
        float[]? bins;
        RgbaColor[]? frame;
        float level;
        float localBrightness;
        string? localTargetDeviceId;
        uint localSequence;
        byte localBrightnessByte;
        byte localBinsFlags;
        ushort[]? encodedFrame;

        lock (gate)
        {
            if (!started)
            {
                return;
            }

            bins = payload.Bins128;
            frame = payload.Frame128x64;
            level = payload.Level;
            localBrightness = brightness;
            localTargetDeviceId = targetDeviceId;
            localBrightnessByte = ToByte01(localBrightness);
            localBinsFlags = payload.BinsFlags;
            encodedFrame = null;

            if (frame is { Length: StreamFrameV2.PixelCount128x64 })
            {
                EnsureFrameBuffersLocked();
                LedFrameDeduplicator.EncodeToRgb565(frame, frameScratchRgb565!);
                if (!LedFrameDeduplicator.ShouldBroadcast(
                    frameScratchRgb565!,
                    hasLastFrame,
                    lastFrameRgb565!,
                    localBrightnessByte,
                    lastFrameBrightness))
                {
                    return;
                }

                (lastFrameRgb565, frameScratchRgb565) = (frameScratchRgb565, lastFrameRgb565);
                lastFrameBrightness = localBrightnessByte;
                hasLastFrame = true;
                encodedFrame = lastFrameRgb565;
            }

            localSequence = ++sequence;
        }

        if (encodedFrame is not null)
        {
            var frameBytes = StreamFrameV2.CreateFrame128x64Rgb565(
                sequence: localSequence,
                timestampQpc: Stopwatch.GetTimestamp(),
                pixels128x64Rgb565: encodedFrame,
                brightness0To255: localBrightnessByte);

            SendFrame(frameBytes, localTargetDeviceId);
            return;
        }

        if (bins is null || bins.Length != StreamFrameV2.BinCount128)
        {
            return;
        }

        Span<byte> binsBytes = stackalloc byte[StreamFrameV2.BinCount128];
        for (var i = 0; i < StreamFrameV2.BinCount128; i++)
        {
            binsBytes[i] = ToByte01(bins[i]);
        }

        var bytes = StreamFrameV2.CreateBins128(
            sequence: localSequence,
            timestampQpc: Stopwatch.GetTimestamp(),
            level0To255: ToByte01(level),
            bins128: binsBytes,
            brightness0To255: localBrightnessByte,
            flags: localBinsFlags);

        SendFrame(bytes, localTargetDeviceId);
    }

    private void EnsureFrameBuffersLocked()
    {
        if (lastFrameRgb565 is null || lastFrameRgb565.Length != StreamFrameV2.PixelCount128x64)
        {
            lastFrameRgb565 = new ushort[StreamFrameV2.PixelCount128x64];
        }

        if (frameScratchRgb565 is null || frameScratchRgb565.Length != StreamFrameV2.PixelCount128x64)
        {
            frameScratchRgb565 = new ushort[StreamFrameV2.PixelCount128x64];
        }
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

    private void SendFrame(byte[] payload, string? localTargetDeviceId)
    {
        if (string.IsNullOrWhiteSpace(localTargetDeviceId))
        {
            deviceServerHost.BroadcastFrame(payload);
            return;
        }

        deviceServerHost.SendFrame(localTargetDeviceId, payload);
    }
}


