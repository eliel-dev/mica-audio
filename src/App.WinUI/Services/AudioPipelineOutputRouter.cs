using MicaAudio.Core.Led;
using Output.Led;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
internal sealed class AudioPipelineOutputRouter
{
    private readonly ILedOutput simulatorLedOutput;
    private readonly ILedOutput esp32s3LedOutput;
    private readonly ILedOutput nullLedOutput;
    private readonly object gate = new();

    private bool hubPreviewEnabled;
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

    public void Configure(bool enableSimulator, float brightness)
    {
        lock (gate)
        {
            hubPreviewEnabled = enableSimulator;
            this.brightness = Math.Clamp(brightness, 0f, 1f);

            var ledConfig = new LedOutputConfig
            {
                Width = LedDefaults.MatrixWidth,
                Height = LedDefaults.MatrixHeight,
                Brightness = this.brightness,
            };

            esp32s3LedOutput.Start(ledConfig);
            esp32s3LedOutput.SetBrightness(this.brightness);

            if (hubPreviewEnabled)
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
            esp32s3LedOutput.Send(payload);

            if (forceSimulator || hubPreviewEnabled)
            {
                simulatorLedOutput.Send(payload);
            }
            else
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
