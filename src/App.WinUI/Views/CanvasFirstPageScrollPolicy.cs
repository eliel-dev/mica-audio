namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-regra-global-de-scroll-canvas-first
internal enum CanvasFirstPageLayoutMode
{
    CompactStacked,
    CanvasFirstDesktop,
}

internal readonly record struct CanvasFirstPageLayoutPlan(
    CanvasFirstPageLayoutMode Mode,
    bool UsePageBodyScroll,
    bool AllowSecondaryPaneInternalScroll,
    double PrimarySurfaceMinHeight,
    double PrimarySurfacePreferredHeight,
    double SecondaryPaneMaxHeight,
    double ScrollViewportRightPadding);

internal static class CanvasFirstPageScrollPolicy
{
    private const double DesktopBreakpoint = 920d;
    private const double MinimumViewportHeight = 640d;
    private const double MinimumPrimarySurfaceHeight = 320d;
    private const double ScrollbarOverlaySafePadding = 16d;

    internal static CanvasFirstPageLayoutMode ResolveMode(double width)
    {
        return width < DesktopBreakpoint
            ? CanvasFirstPageLayoutMode.CompactStacked
            : CanvasFirstPageLayoutMode.CanvasFirstDesktop;
    }

    internal static CanvasFirstPageLayoutPlan Resolve(double width, double height)
    {
        var mode = ResolveMode(width);
        var effectiveHeight = Math.Max(height, MinimumViewportHeight);

        return mode switch
        {
            CanvasFirstPageLayoutMode.CompactStacked => new CanvasFirstPageLayoutPlan(
                Mode: mode,
                UsePageBodyScroll: true,
                AllowSecondaryPaneInternalScroll: true,
                PrimarySurfaceMinHeight: MinimumPrimarySurfaceHeight,
                PrimarySurfacePreferredHeight: Math.Clamp((effectiveHeight - 220d) * 0.52d, MinimumPrimarySurfaceHeight, 420d),
                SecondaryPaneMaxHeight: Math.Clamp((effectiveHeight - 180d) * 0.32d, 220d, 320d),
                ScrollViewportRightPadding: ScrollbarOverlaySafePadding),
            _ => new CanvasFirstPageLayoutPlan(
                Mode: mode,
                UsePageBodyScroll: true,
                AllowSecondaryPaneInternalScroll: true,
                PrimarySurfaceMinHeight: MinimumPrimarySurfaceHeight,
                PrimarySurfacePreferredHeight: Math.Clamp((effectiveHeight - 200d) * 0.58d, MinimumPrimarySurfaceHeight, 520d),
                SecondaryPaneMaxHeight: Math.Clamp((effectiveHeight - 180d) * 0.35d, 220d, 360d),
                ScrollViewportRightPadding: ScrollbarOverlaySafePadding),
        };
    }
}
