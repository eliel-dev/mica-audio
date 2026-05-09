using Panels.Composition.Models;

namespace Panels.Composition.ServerSide;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Classifies a PanelDefinition by whether the MicaAudio.Server can render it
// autonomously (after the WinUI client closes) or whether it depends on a
// data source that lives only in the desktop client (audio loopback, HWInfo).
//
// The first server-side iteration only handles Clock widgets standalone.
// GIF/Image (gifhub75) and other future widgets are still client-only until
// their decoders + media uploads are implemented server-side.
public enum PanelServerCapability
{
    /// <summary>Every widget in the panel can be composed by the server.</summary>
    ServerCapable,

    /// <summary>At least one widget needs data only the WinUI client can provide.</summary>
    RequiresClient,

    /// <summary>Panel has no widgets at all — server can render an empty frame.</summary>
    Empty,
}

public static class PanelServerCapabilityClassifier
{
    private static readonly HashSet<string> ServerCapableAppIds =
        new(StringComparer.OrdinalIgnoreCase) { "analogclock" };

    public static PanelServerCapability Classify(PanelDefinition? panel)
    {
        if (panel is null || panel.Widgets is null || panel.Widgets.Count == 0)
        {
            return PanelServerCapability.Empty;
        }

        foreach (var widget in panel.Widgets)
        {
            var appId = widget.AppId?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!ServerCapableAppIds.Contains(appId))
            {
                return PanelServerCapability.RequiresClient;
            }
        }

        return PanelServerCapability.ServerCapable;
    }
}
