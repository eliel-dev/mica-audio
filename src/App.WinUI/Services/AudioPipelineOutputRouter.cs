using MicaAudio.Core.Led;
using Output.Led;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/output-led.md#atualizacao-2026-03-toggle-hub75-como-gate-do-device-output
internal sealed class AudioPipelineOutputRouter
{
    private readonly ILedOutput simulatorLedOutput;
    private readonly ILedOutput esp32s3LedOutput;
    private readonly ILedOutput nullLedOutput;
    private readonly object gate = new();

    private bool simulatorEnabled;
    private bool hub75DeviceEnabled;
    private float brightness = LedDefaults.Brightness;

    public AudioPipelineOutputRouter(
        ILedOutput simulatorLedOutput,
        ILedOutput esp32s3LedOutput,
        ILedOutput nullLedOutput)
    {
        this.simulatorLedOutput = simulatorLedOutput;
        this.esp32s3LedOutput = esp32s3LedOutput;
        this.nullLedOutput = nullLedOutput;
    }

    public void Configure(bool enableSimulator, bool enableHub75DeviceOutput, float brightness)
    {
        lock (gate)
        {
            simulatorEnabled = enableSimulator;
            hub75DeviceEnabled = enableHub75DeviceOutput;
            this.brightness = Math.Clamp(brightness, 0f, 1f);

            var ledConfig = new LedOutputConfig
            {
                Width = LedDefaults.MatrixWidth,
                Height = LedDefaults.MatrixHeight,
                Brightness = this.brightness,
            };

            if (hub75DeviceEnabled)
            {
                esp32s3LedOutput.Start(ledConfig);
                esp32s3LedOutput.SetBrightness(this.brightness);
            }
            else
            {
                esp32s3LedOutput.Stop();
            }

            if (simulatorEnabled)
            {
                simulatorLedOutput.Start(ledConfig);
                simulatorLedOutput.SetBrightness(this.brightness);
            }
            else
            {
                simulatorLedOutput.Stop();
            }

            nullLedOutput.Start(ledConfig);
            nullLedOutput.SetBrightness(this.brightness);
        }
    }

    public void Dispatch(LedPayload payload, bool forceSimulator = false)
    {
        ArgumentNullException.ThrowIfNull(payload);

        lock (gate)
        {
            if (hub75DeviceEnabled)
            {
                esp32s3LedOutput.Send(payload);
            }

            if (forceSimulator || simulatorEnabled)
            {
                simulatorLedOutput.Send(payload);
            }
            else if (!hub75DeviceEnabled)
            {
                nullLedOutput.Send(payload);
            }
        }
    }

    public void StopAll()
    {
        lock (gate)
        {
            simulatorLedOutput.Stop();
            esp32s3LedOutput.Stop();
            nullLedOutput.Stop();
        }
    }
}
