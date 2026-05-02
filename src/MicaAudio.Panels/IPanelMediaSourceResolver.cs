using Device.Protocol.Models;

namespace MicaAudio.Panels;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
public interface IPanelMediaSourceResolver
{
    Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(PanelWidgetItem widget, CancellationToken cancellationToken = default);
}
