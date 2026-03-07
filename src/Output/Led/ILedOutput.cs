using System.Diagnostics.CodeAnalysis;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

// DOCS: docs/wiki/modules/output-led.md#modulo-output-led
public interface ILedOutput
{
    bool IsAvailable { get; }

    void Start(LedOutputConfig config);

    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "O verbo Stop ja e o contrato de ciclo de vida estabelecido para outputs LED e renomea-lo causaria churn evitavel entre assemblies consumidores.")]
    void Stop();

    void Send(LedPayload payload);

    void SetBrightness(float value);
}
