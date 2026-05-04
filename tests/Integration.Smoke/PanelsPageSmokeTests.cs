using System.Collections.Generic;
using System.Reflection;
using App.WinUI.Models.Apps;
using App.WinUI.Models.Panels;
using App.WinUI.Views;
using App.WinUI.Views.Controls;
using MicaAudio.Core.Presets;
using Windows.Foundation;

namespace Integration.Smoke;

public sealed class PanelsPageSmokeTests
{
    [Fact]
    public void PanelsPageShouldDeclareGalleryAndEditorFields()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        Assert.NotNull(typeof(PanelsPage).GetField("PanelsGalleryGrid", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("GalleryView", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("EditorView", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("NewPanelButton", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("EditorBackButton", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("EditorCanvas", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("TargetDeviceCombo", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("WidgetLibraryList", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("WidgetLibrarySearchBox", flags));
        Assert.Null(typeof(PanelsPage).GetField("WidgetLibrarySummaryText", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("WidgetModifiersPanel", flags));
        Assert.Null(typeof(PanelsPage).GetField("WidgetLayersList", flags));
        Assert.Null(typeof(PanelsPage).GetField("SendWidgetBackwardButton", flags));
        Assert.Null(typeof(PanelsPage).GetField("BringWidgetForwardButton", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("EditorNameText", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("EditorPreviewToggle", flags));
        Assert.Null(typeof(PanelsPage).GetField("PanelWidthBox", flags));
        Assert.Null(typeof(PanelsPage).GetField("PanelHeightBox", flags));
        Assert.Null(typeof(PanelsPage).GetField("WidgetXBox", flags));
        Assert.Null(typeof(PanelsPage).GetField("WidgetYBox", flags));
        Assert.Null(typeof(PanelsPage).GetField("WidgetWidthBox", flags));
        Assert.Null(typeof(PanelsPage).GetField("WidgetHeightBox", flags));
    }

    [Fact]
    public void PanelsPageShouldKeepGalleryEditorAndPlaybackMethods()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        Assert.NotNull(typeof(PanelsPage).GetMethod("LoadPanelsAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("LoadCurrentPanelAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("StopPlaybackAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("RefreshPreviewSessionAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("OpenEditorAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("TryReturnToGalleryAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("OnCanvasDrop", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("ApplyWidgetLibraryFilter", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("ResolveWidgetDefaultValuesAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("BuildCatalogDefaultWidgetValues", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("TryResolveDraggedWidgetAppId", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("OnEditorMoveSelectedWidgetBackwardRequested", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("OnEditorMoveSelectedWidgetForwardRequested", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("OnEditorCycleOverlappingWidgetRequested", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("QueuePosterGenerationAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("SetPanelActivationAsync", flags));
        Assert.Null(typeof(PanelsPage).GetMethod("BringSelectedWidgetToFront", flags));
        Assert.Null(typeof(PanelsPage).GetMethod("RegenerateAllThumbnailsAsync", flags));
        Assert.Null(typeof(PanelsPage).GetMethod("OnPanelsGalleryContainerContentChanging", flags));
    }

    [Fact]
    public void Hub75EditorShouldExposeBoundsEventInsteadOfMoveEvent()
    {
        Assert.NotNull(typeof(Hub75PanelEditorControl).GetEvent("WidgetBoundsChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(typeof(Hub75PanelEditorControl).GetEvent("WidgetMoved", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    [Fact]
    public void Hub75EditorShouldExposeLayerToolbarCommandEvents()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Assert.NotNull(typeof(Hub75PanelEditorControl).GetEvent("MoveSelectedWidgetBackwardRequested", flags));
        Assert.NotNull(typeof(Hub75PanelEditorControl).GetEvent("MoveSelectedWidgetForwardRequested", flags));
        Assert.NotNull(typeof(Hub75PanelEditorControl).GetEvent("CycleOverlappingWidgetRequested", flags));
        Assert.Null(typeof(Hub75PanelEditorControl).GetEvent("WidgetMoved", flags));
    }

    [Fact]
    public void PanelsPageShouldResolveDragPayloadFromPropertyOrText()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("TryResolveDraggedWidgetAppId", flags);
        Assert.NotNull(method);

        var propertyArgs = new object?[] { "gifhub75", null, null };
        var propertyResolved = (bool)method.Invoke(null, propertyArgs)!;
        Assert.True(propertyResolved);
        Assert.Equal("gifhub75", propertyArgs[2]);

        var textArgs = new object?[] { null, " analogclock ", null };
        var textResolved = (bool)method.Invoke(null, textArgs)!;
        Assert.True(textResolved);
        Assert.Equal("analogclock", textArgs[2]);

        var invalidArgs = new object?[] { "  ", null, null };
        var invalidResolved = (bool)method.Invoke(null, invalidArgs)!;
        Assert.False(invalidResolved);
    }

    [Fact]
    public void PanelsGalleryCardControlShouldExistForGalleryTemplatePath()
    {
        Assert.NotNull(typeof(PanelGalleryCardControl));
    }

    [Fact]
    public void WidgetRailCardControlShouldExposeStaticVisualSpec()
    {
        var item = new AppCatalogItem
        {
            Id = "analogclock",
            Name = "Relogio",
            Category = "relogio",
            PackageName = "analogclock",
        };

        var visual = ResolveWidgetRailVisual(item, "Relogio");

        Assert.Equal("clock", ReadVisualSpecString(visual, "Kind"));
        Assert.Equal("15:43", ReadVisualSpecString(visual, "PrimaryToken"));
    }

    [Theory]
    [InlineData("gifhub75", "GIF", "midia", "media", "GIF")]
    [InlineData("spectrum", "Spectrum", "audio", "audio", "")]
    public void WidgetRailCardControlShouldResolveStaticVisualKinds(
        string appId,
        string name,
        string category,
        string expectedVisualKind,
        string expectedVisualToken)
    {
        var item = new AppCatalogItem
        {
            Id = appId,
            Name = name,
            Category = category,
            PackageName = appId,
        };

        var visual = ResolveWidgetRailVisual(item, name);

        Assert.Equal(expectedVisualKind, ReadVisualSpecString(visual, "Kind"));
        Assert.Equal(expectedVisualToken, ReadVisualSpecString(visual, "PrimaryToken"));
    }

    [Fact]
    public void PanelsPageShouldResolveDraggedCatalogItemFromWidgetLibraryEntry()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("TryResolveDraggedCatalogItem", flags);
        Assert.NotNull(method);

        var entry = CreateWidgetLibraryEntry(
            new AppCatalogItem
            {
                Id = "gifhub75",
                Name = "GIF",
                Category = "midia",
                PackageName = "gifhub75",
            },
            isSupported: true);

        var args = new object?[] { new object[] { entry }, null };
        var resolved = (bool)method!.Invoke(null, args)!;

        Assert.True(resolved);
        Assert.NotNull(args[1]);
        Assert.Equal("gifhub75", ReadWidgetLibraryEntryItemId(args[1]!));
    }

    [Fact]
    public void PanelDefinitionShouldMoveWidgetOneStepForwardAndNormalizeZOrder()
    {
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition { WidgetId = "bottom", AppId = "a", ZIndex = 10 },
                new PanelWidgetDefinition { WidgetId = "middle", AppId = "b", ZIndex = 20 },
                new PanelWidgetDefinition { WidgetId = "top", AppId = "c", ZIndex = 30 },
            ],
        };

        var changed = panel.TryMoveWidgetForward("middle");

        Assert.True(changed);
        Assert.Collection(
            panel.Widgets,
            widget => Assert.Equal(("bottom", 0), (widget.WidgetId, widget.ZIndex)),
            widget => Assert.Equal(("top", 1), (widget.WidgetId, widget.ZIndex)),
            widget => Assert.Equal(("middle", 2), (widget.WidgetId, widget.ZIndex)));
    }

    [Fact]
    public void PanelDefinitionShouldMoveWidgetOneStepBackwardAndNormalizeZOrder()
    {
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition { WidgetId = "bottom", AppId = "a", ZIndex = 0 },
                new PanelWidgetDefinition { WidgetId = "middle", AppId = "b", ZIndex = 1 },
                new PanelWidgetDefinition { WidgetId = "top", AppId = "c", ZIndex = 2 },
            ],
        };

        var changed = panel.TryMoveWidgetBackward("middle");

        Assert.True(changed);
        Assert.Collection(
            panel.Widgets,
            widget => Assert.Equal(("middle", 0), (widget.WidgetId, widget.ZIndex)),
            widget => Assert.Equal(("bottom", 1), (widget.WidgetId, widget.ZIndex)),
            widget => Assert.Equal(("top", 2), (widget.WidgetId, widget.ZIndex)));
    }

    [Fact]
    public void PanelDefinitionShouldNotMoveExtremeWidgets()
    {
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition { WidgetId = "bottom", AppId = "a", ZIndex = 0 },
                new PanelWidgetDefinition { WidgetId = "top", AppId = "b", ZIndex = 1 },
            ],
        };

        Assert.False(panel.TryMoveWidgetBackward("bottom"));
        Assert.False(panel.TryMoveWidgetForward("top"));
        Assert.Collection(
            panel.Widgets,
            widget => Assert.Equal(("bottom", 0), (widget.WidgetId, widget.ZIndex)),
            widget => Assert.Equal(("top", 1), (widget.WidgetId, widget.ZIndex)));
    }

    [Fact]
    public void PanelDefinitionShouldExposeWidgetsInVisualOrderTopFirst()
    {
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition { WidgetId = "bottom", AppId = "a", ZIndex = 0 },
                new PanelWidgetDefinition { WidgetId = "middle", AppId = "b", ZIndex = 1 },
                new PanelWidgetDefinition { WidgetId = "top", AppId = "c", ZIndex = 2 },
            ],
        };

        var visualOrder = panel.GetWidgetsInVisualOrderTopFirst();

        Assert.Collection(
            visualOrder,
            widget => Assert.Equal("top", widget.WidgetId),
            widget => Assert.Equal("middle", widget.WidgetId),
            widget => Assert.Equal("bottom", widget.WidgetId));
        Assert.Equal(2, panel.Widgets[2].ZIndex);
    }

    [Fact]
    public void PanelDefinitionShouldResolveOverlappingWidgetsOnlyWhenAreaIntersects()
    {
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition { WidgetId = "base", AppId = "a", X = 0, Y = 0, Width = 32, Height = 32, ZIndex = 0 },
                new PanelWidgetDefinition { WidgetId = "overlap", AppId = "b", X = 16, Y = 8, Width = 32, Height = 24, ZIndex = 2 },
                new PanelWidgetDefinition { WidgetId = "edge-touch", AppId = "c", X = 32, Y = 0, Width = 16, Height = 16, ZIndex = 1 },
            ],
        };

        var overlapping = panel.GetOverlappingWidgetsInVisualOrderTopFirst("base");

        Assert.Collection(
            overlapping,
            widget => Assert.Equal("overlap", widget.WidgetId));
    }

    [Fact]
    public void PanelDefinitionShouldCycleOverlappingWidgetsInVisualOrderAndWrap()
    {
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition { WidgetId = "bottom", AppId = "a", X = 4, Y = 4, Width = 40, Height = 40, ZIndex = 0 },
                new PanelWidgetDefinition { WidgetId = "middle", AppId = "b", X = 8, Y = 8, Width = 40, Height = 40, ZIndex = 1 },
                new PanelWidgetDefinition { WidgetId = "top", AppId = "c", X = 12, Y = 12, Width = 40, Height = 40, ZIndex = 2 },
            ],
        };

        Assert.True(panel.TryGetNextOverlappingWidgetId("top", out var nextFromTop));
        Assert.Equal("middle", nextFromTop);

        Assert.True(panel.TryGetNextOverlappingWidgetId("middle", out var nextFromMiddle));
        Assert.Equal("bottom", nextFromMiddle);

        Assert.True(panel.TryGetNextOverlappingWidgetId("bottom", out var nextFromBottom));
        Assert.Equal("top", nextFromBottom);
    }

    [Theory]
    [InlineData(332d, 300d, 150d)]
    [InlineData(260d, 228d, 114d)]
    public void PanelGalleryCardControlShouldResolveHub75PreviewSize(double cardWidth, double expectedWidth, double expectedHeight)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var method = typeof(PanelGalleryCardControl).GetMethod("ResolvePreviewSize", flags);
        Assert.NotNull(method);

        var size = Assert.IsType<Size>(method!.Invoke(null, [cardWidth])!);
        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }

    [Fact]
    public void PanelGalleryItemStateShouldExposeBindableFrameAndWidth()
    {
        const BindingFlags flags = BindingFlags.NonPublic;
        var stateType = typeof(PanelsPage).GetNestedType("PanelGalleryItemState", flags);
        Assert.NotNull(stateType);

        var ctor = stateType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var frame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), 128 * 64).ToArray();
        var state = ctor.Invoke(
        [
            "panel-1",
            "Painel 1",
            2,
            332d,
            frame,
            (Func<string, Task>)(_ => Task.CompletedTask),
            (Func<string, Task>)(_ => Task.CompletedTask),
            (Func<string, Task>)(_ => Task.CompletedTask),
            (Func<string, bool, Task>)((_, _) => Task.CompletedTask),
        ]);

        Assert.Equal(frame, stateType.GetProperty("Frame")!.GetValue(state));
        Assert.Equal(332d, stateType.GetProperty("CardWidth")!.GetValue(state));
    }

    [Theory]
    [InlineData(919d, "CompactStacked")]
    [InlineData(920d, "CanvasFirstDesktop")]
    [InlineData(1366d, "CanvasFirstDesktop")]
    [InlineData(1440d, "CanvasFirstDesktop")]
    [InlineData(1920d, "CanvasFirstDesktop")]
    public void PanelsPageShouldResolveEditorAdaptiveLayoutMode(double width, string expectedMode)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("ResolveEditorAdaptiveLayoutMode", flags);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [width]);
        Assert.NotNull(result);
        Assert.Equal(expectedMode, result!.ToString());
    }

    [Theory]
    [InlineData(880d, 900d, "CompactStacked", true, true, 320d, 353.6d, 230.4d, 16d)]
    [InlineData(1100d, 700d, "CanvasFirstDesktop", true, true, 320d, 320d, 220d, 16d)]
    [InlineData(1366d, 768d, "CanvasFirstDesktop", true, true, 320d, 329.44d, 220d, 16d)]
    [InlineData(1920d, 1080d, "CanvasFirstDesktop", true, true, 320d, 510.4d, 315d, 16d)]
    public void PanelsPageShouldResolveEditorLayoutPlan(
        double width,
        double height,
        string expectedMode,
        bool expectedUsePageBodyScroll,
        bool expectedAllowSecondaryPaneInternalScroll,
        double expectedPrimarySurfaceMinHeight,
        double expectedPrimarySurfacePreferredHeight,
        double expectedSecondaryPaneMaxHeight,
        double expectedScrollViewportRightPadding)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("ResolveEditorLayoutPlan", flags);
        Assert.NotNull(method);

        var plan = method!.Invoke(null, [width, height]);
        Assert.NotNull(plan);

        var planType = plan!.GetType();
        Assert.Equal(expectedMode, planType.GetProperty("Mode")!.GetValue(plan)!.ToString());
        Assert.Equal(expectedUsePageBodyScroll, Assert.IsType<bool>(planType.GetProperty("UsePageBodyScroll")!.GetValue(plan)));
        Assert.Equal(expectedAllowSecondaryPaneInternalScroll, Assert.IsType<bool>(planType.GetProperty("AllowSecondaryPaneInternalScroll")!.GetValue(plan)));
        Assert.Equal(expectedPrimarySurfaceMinHeight, Assert.IsType<double>(planType.GetProperty("PrimarySurfaceMinHeight")!.GetValue(plan)));
        Assert.Equal(expectedPrimarySurfacePreferredHeight, Assert.IsType<double>(planType.GetProperty("PrimarySurfacePreferredHeight")!.GetValue(plan)), 2);
        Assert.Equal(expectedSecondaryPaneMaxHeight, Assert.IsType<double>(planType.GetProperty("SecondaryPaneMaxHeight")!.GetValue(plan)), 1);
        Assert.Equal(expectedScrollViewportRightPadding, Assert.IsType<double>(planType.GetProperty("ScrollViewportRightPadding")!.GetValue(plan)));
    }

    [Theory]
    [InlineData(880d, 900d, "CompactStacked", 0, 0, 1, 0, 2, 1, 0, 1)]
    [InlineData(1100d, 700d, "CanvasFirstDesktop", 0, 0, 2, 0, 1, 1, 1, 1)]
    [InlineData(1366d, 768d, "CanvasFirstDesktop", 0, 0, 2, 0, 1, 1, 1, 1)]
    [InlineData(1920d, 1080d, "CanvasFirstDesktop", 0, 0, 2, 0, 1, 1, 1, 1)]
    public void PanelsPageShouldResolveEditorPaneLayout(
        double width,
        double height,
        string expectedMode,
        int expectedWidgetLibraryColumn,
        int expectedWidgetLibraryRow,
        int expectedWidgetLibraryColumnSpan,
        int expectedInspectorColumn,
        int expectedInspectorRow,
        int expectedInspectorColumnSpan,
        int expectedCanvasColumn,
        int expectedCanvasRow)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("ResolveEditorPaneLayout", flags);
        Assert.NotNull(method);

        var layout = method!.Invoke(null, [width, height]);
        Assert.NotNull(layout);

        var layoutType = layout!.GetType();
        Assert.Equal(expectedMode, layoutType.GetProperty("Mode")!.GetValue(layout)!.ToString());
        Assert.Equal(expectedWidgetLibraryColumn, Assert.IsType<int>(layoutType.GetProperty("WidgetLibraryColumn")!.GetValue(layout)));
        Assert.Equal(expectedWidgetLibraryRow, Assert.IsType<int>(layoutType.GetProperty("WidgetLibraryRow")!.GetValue(layout)));
        Assert.Equal(expectedWidgetLibraryColumnSpan, Assert.IsType<int>(layoutType.GetProperty("WidgetLibraryColumnSpan")!.GetValue(layout)));
        Assert.Equal(expectedInspectorColumn, Assert.IsType<int>(layoutType.GetProperty("InspectorColumn")!.GetValue(layout)));
        Assert.Equal(expectedInspectorRow, Assert.IsType<int>(layoutType.GetProperty("InspectorRow")!.GetValue(layout)));
        Assert.Equal(expectedInspectorColumnSpan, Assert.IsType<int>(layoutType.GetProperty("InspectorColumnSpan")!.GetValue(layout)));
        Assert.Equal(expectedCanvasColumn, Assert.IsType<int>(layoutType.GetProperty("CanvasColumn")!.GetValue(layout)));
        Assert.Equal(expectedCanvasRow, Assert.IsType<int>(layoutType.GetProperty("CanvasRow")!.GetValue(layout)));
    }

    [Theory]
    [InlineData("analogclock", "Relogio", "Relogio")]
    [InlineData("gifhub75", "GIF", "Foto / GIF")]
    [InlineData("news", "Ticker", "Ticker")]
    public void PanelsPageShouldResolveWidgetLibraryShortLabel(string appId, string name, string expectedLabel)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("ResolveWidgetLibraryShortLabel", flags);
        Assert.NotNull(method);

        var item = new AppCatalogItem
        {
            Id = appId,
            Name = name,
            PackageName = appId,
        };

        var label = Assert.IsType<string>(method!.Invoke(null, [item])!);
        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [InlineData("analogclock", "relogio", "Relogio")]
    [InlineData("gifhub75", "midia", "Midia")]
    [InlineData("weather", "clima", "Clima")]
    [InlineData("spectrum", "audio", "Audio")]
    public void PanelsPageShouldResolveWidgetLibraryCategoryLabel(string appId, string category, string expectedLabel)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(PanelsPage).GetMethod("ResolveWidgetLibraryCategoryLabel", flags);
        Assert.NotNull(method);

        var item = new AppCatalogItem
        {
            Id = appId,
            Name = appId,
            Category = category,
            PackageName = appId,
        };

        var label = Assert.IsType<string>(method!.Invoke(null, [item])!);
        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [InlineData(880d, 900d, "CompactStacked")]
    [InlineData(1100d, 700d, "CanvasFirstDesktop")]
    [InlineData(1366d, 768d, "CanvasFirstDesktop")]
    [InlineData(1920d, 1080d, "CanvasFirstDesktop")]
    public void CanvasFirstPageScrollPolicyShouldPreferPageBodyScroll(
        double width,
        double height,
        string expectedMode)
    {
        var plan = CanvasFirstPageScrollPolicy.Resolve(width, height);

        Assert.Equal(expectedMode, plan.Mode.ToString());
        Assert.True(plan.UsePageBodyScroll);
        Assert.True(plan.AllowSecondaryPaneInternalScroll);
        Assert.Equal(320d, plan.PrimarySurfaceMinHeight);
        Assert.True(plan.PrimarySurfacePreferredHeight >= plan.PrimarySurfaceMinHeight);
        Assert.True(plan.SecondaryPaneMaxHeight >= 220d);
        Assert.Equal(16d, plan.ScrollViewportRightPadding);
    }

    private static object CreateWidgetLibraryEntry(AppCatalogItem item, bool isSupported)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var entryType = typeof(PanelsPage).GetNestedType("WidgetLibraryEntry", BindingFlags.NonPublic);
        Assert.NotNull(entryType);

        var ctor = entryType!.GetConstructors(flags).Single(constructor => constructor.GetParameters().Length == 4);
        return ctor.Invoke([item, isSupported, null, null]);
    }

    private static string ReadWidgetLibraryEntryItemId(object entry)
    {
        var entryType = entry.GetType();
        var item = Assert.IsType<AppCatalogItem>(entryType.GetProperty("Item")!.GetValue(entry));
        return item.Id;
    }

    private static object ResolveWidgetRailVisual(AppCatalogItem item, string displayName)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(WidgetRailCardControl).GetMethod("ResolveVisual", flags);
        Assert.NotNull(method);
        return method!.Invoke(null, [item, displayName])!;
    }

    private static string ReadVisualSpecString(object spec, string propertyName)
    {
        return Assert.IsType<string>(spec.GetType().GetProperty(propertyName)!.GetValue(spec));
    }
}
