using MicaAudio.Core.Led;

namespace Output.Led;

public sealed class NullLedOutput : ILedOutput
{
    public bool IsAvailable => true;

    public void Start(LedOutputConfig config)
    {
    }

    public void Stop()
    {
    }

    public void Send(LedPayload payload)
    {
    }

    public void SetBrightness(float value)
    {
    }
}
