using MicaAudio.Core.Presets;
using Panels.Composition.Drawing;
using Panels.Composition.Models;

namespace Panels.Composition.ServerSide;

// Server-side runtime for the "gifhub75" widget app-id. Handles single-file
// GIFs/PNG/JPG/BMP and folder-based slideshows.
//
// RuntimeState keys consumed (in priority order):
//   mediaIds  — comma-separated list of media filenames (slideshow). Each entry
//               must already exist on disk under {mediaDirectory}/{entry}.
//   mediaId   — legacy single-file fallback when mediaIds is absent.
//
// ConfigValues consumed:
//   scaleMode         — "Fit" | "Fill" | "Stretch" (default Fit)
//   slideshowInterval — "10s" | "500ms" | "2m" | "1500" (default 5s)
//                       Plain numbers are interpreted as milliseconds.
//
// Each media entry is decoded once at TryCreate and the decoded frames stay in
// memory so the render hot-path is allocation-free. The slideshow rotates
// through entries on slideshowInterval; within each slot, animated GIFs loop
// at their natural frame timing.
public sealed class ServerGifWidgetRuntime : IServerWidgetRuntime
{
    private const int DefaultSlideshowIntervalMs = 5000;

    private readonly PanelWidgetDefinition widget;
    private readonly IReadOnlyList<MediaSequence> sequences;
    private readonly int slideshowIntervalMs;
    private readonly DateTimeOffset startedAt;

    private ServerGifWidgetRuntime(
        PanelWidgetDefinition widget,
        IReadOnlyList<MediaSequence> sequences,
        int slideshowIntervalMs)
    {
        this.widget = widget;
        this.sequences = sequences;
        this.slideshowIntervalMs = slideshowIntervalMs;
        startedAt = DateTimeOffset.UtcNow;
    }

    public static ServerGifWidgetRuntime? TryCreate(PanelWidgetDefinition widget, string? mediaDirectory)
    {
        ArgumentNullException.ThrowIfNull(widget);

        if (string.IsNullOrWhiteSpace(mediaDirectory))
        {
            return null;
        }

        var mediaIds = ResolveMediaIds(widget);
        if (mediaIds.Count == 0)
        {
            return null;
        }

        var scaleMode = ParseScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);

        var sequences = new List<MediaSequence>(mediaIds.Count);
        foreach (var mediaId in mediaIds)
        {
            var sanitized = SanitizeMediaId(mediaId);
            if (string.IsNullOrEmpty(sanitized))
            {
                continue;
            }

            var filePath = Path.Combine(mediaDirectory, sanitized);
            var sequence = ServerMediaDecoder.DecodeFile(filePath, widget.Width, widget.Height, scaleMode);
            if (!sequence.IsEmpty)
            {
                sequences.Add(sequence);
            }
        }

        if (sequences.Count == 0)
        {
            return null;
        }

        var intervalMs = sequences.Count > 1
            ? ParseSlideshowIntervalMs(widget.ConfigValues.TryGetValue("slideshowInterval", out var rawInterval) ? rawInterval : null)
            : int.MaxValue;

        return new ServerGifWidgetRuntime(widget.Clone(), sequences, intervalMs);
    }

    public string WidgetId => widget.WidgetId;

    public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
    {
        if (sequences.Count == 0)
        {
            return;
        }

        var elapsedMs = (long)(utcNow - startedAt).TotalMilliseconds;
        if (elapsedMs < 0)
        {
            elapsedMs = 0;
        }

        // Slideshow: rotate through sequences each slideshowIntervalMs slot.
        var sequenceIndex = sequences.Count == 1
            ? 0
            : (int)((elapsedMs / slideshowIntervalMs) % sequences.Count);

        var current = sequences[sequenceIndex];
        if (current.IsEmpty)
        {
            return;
        }

        // Within the current slot, animation timing restarts at slot boundary.
        // For animated GIFs this means the loop seeks back to frame 0 each time
        // the slideshow advances to the next entry.
        var slotElapsedMs = sequences.Count == 1 ? elapsedMs : elapsedMs % slideshowIntervalMs;
        var frameIndex = ResolveFrameIndex(current, slotElapsedMs);
        var frame = current.Frames[frameIndex];

        PanelsMatrixDrawHelpers.BlitDirect(
            frame.Pixels,
            frame.FrameWidth,
            frame.FrameHeight,
            targetFrame,
            panelWidth,
            panelHeight,
            widget.X,
            widget.Y);
    }

    public void Dispose()
    {
        // Frames are managed memory; GC handles cleanup.
    }

    private static List<string> ResolveMediaIds(PanelWidgetDefinition widget)
    {
        if (widget.RuntimeState.TryGetValue("mediaIds", out var multi) && !string.IsNullOrWhiteSpace(multi))
        {
            return multi
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        if (widget.RuntimeState.TryGetValue("mediaId", out var single) && !string.IsNullOrWhiteSpace(single))
        {
            return new List<string> { single.Trim() };
        }

        return new List<string>();
    }

    private static int ResolveFrameIndex(MediaSequence sequence, long elapsedMs)
    {
        if (sequence.Frames.Count == 1)
        {
            return 0;
        }

        var totalMs = 0L;
        foreach (var frame in sequence.Frames)
        {
            totalMs += frame.DurationMs;
        }

        if (totalMs <= 0)
        {
            return 0;
        }

        var wrappedMs = elapsedMs % totalMs;
        var accumulated = 0L;
        for (var i = 0; i < sequence.Frames.Count; i++)
        {
            accumulated += sequence.Frames[i].DurationMs;
            if (wrappedMs < accumulated)
            {
                return i;
            }
        }

        return sequence.Frames.Count - 1;
    }

    private static MediaScaleMode ParseScaleMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return MediaScaleMode.Fit;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "fill" => MediaScaleMode.Fill,
            "stretch" => MediaScaleMode.Stretch,
            _ => MediaScaleMode.Fit,
        };
    }

    /// <summary>
    /// Accepts: "10s", "500ms", "2m", or a plain number (interpreted as ms).
    /// Returns 5000 ms when the input is missing or unparseable.
    /// Clamped to a minimum of 100 ms to keep the loop honest.
    /// </summary>
    internal static int ParseSlideshowIntervalMs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultSlideshowIntervalMs;
        }

        var trimmed = raw.Trim().ToLowerInvariant();

        if (trimmed.EndsWith("ms", StringComparison.Ordinal)
            && int.TryParse(trimmed.AsSpan(0, trimmed.Length - 2), out var ms))
        {
            return Math.Max(100, ms);
        }

        if (trimmed.EndsWith('s')
            && int.TryParse(trimmed.AsSpan(0, trimmed.Length - 1), out var seconds))
        {
            return Math.Max(100, seconds * 1000);
        }

        if (trimmed.EndsWith('m')
            && int.TryParse(trimmed.AsSpan(0, trimmed.Length - 1), out var minutes))
        {
            return Math.Max(100, minutes * 60_000);
        }

        if (int.TryParse(trimmed, out var rawMs))
        {
            return Math.Max(100, rawMs);
        }

        return DefaultSlideshowIntervalMs;
    }

    private static string SanitizeMediaId(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_')
            {
                sb.Append(ch);
            }
        }

        var sanitized = sb.ToString().TrimStart('.');
        return string.IsNullOrEmpty(sanitized) ? string.Empty : sanitized;
    }
}
