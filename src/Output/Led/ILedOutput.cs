using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Led;

public interface ILedOutput
{
    bool IsAvailable { get; }

    void Start(LedOutputConfig config);

    void Stop();

    void Send(LedPayload payload);

    void SetBrightness(float value);
}
