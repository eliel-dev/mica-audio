using MicaAudio.Core.Presets;

namespace Panels.Composition.ServerSide;

public interface IServerWidgetRuntime : IDisposable
{
    string WidgetId { get; }

    void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight);
}
