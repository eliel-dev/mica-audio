using Panels.Composition.Models;

namespace Panels.Composition.ServerSide;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Classifies a PanelDefinition by whether the MicaAudio.Server can render it
// autonomously (after the WinUI client closes) or whether it depends on a
// data source that lives only in the desktop client (audio loopback, HWInfo).
//
// Server-capable widgets (V1):
//   analogclock — draws clock from system time, no external data.
//   gifhub75    — plays GIF/image previously uploaded via PUT /api/v1/admin/devices/{id}/media.
//                 ServerCapable ONLY when RuntimeState["mediaId"] is present; otherwise
//                 the media file is not on the server and the widget RequiresClient.
//
// Client-only widgets (RequiresClient):
//   Everything else (audio visualizer, PC metrics, weather, …).
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
    public static PanelServerCapability Classify(PanelDefinition? panel)
    {
        if (panel is null || panel.Widgets is null || panel.Widgets.Count == 0)
        {
            return PanelServerCapability.Empty;
        }

        foreach (var widget in panel.Widgets)
        {
            var appId = widget.AppId?.Trim().ToLowerInvariant() ?? string.Empty;

            switch (appId)
            {
                case "analogclock":
                    // Pure time-based — always server-capable.
                    continue;

                case "gifhub75":
                    // Server-capable when media was already uploaded.
                    //   mediaIds (plural, comma-separated) — slideshow with N images
                    //   mediaId  (legacy, single)         — single file
                    // Without either, the server cannot find files to decode.
                    var hasMulti = widget.RuntimeState.TryGetValue("mediaIds", out var mediaIds)
                                && !string.IsNullOrWhiteSpace(mediaIds);
                    var hasSingle = widget.RuntimeState.TryGetValue("mediaId", out var mediaId)
                                 && !string.IsNullOrWhiteSpace(mediaId);
                    if (!hasMulti && !hasSingle)
                    {
                        return PanelServerCapability.RequiresClient;
                    }
                    continue;

                default:
                    return PanelServerCapability.RequiresClient;
            }
        }

        return PanelServerCapability.ServerCapable;
    }
}
