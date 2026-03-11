using System.Reflection;
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
        Assert.NotNull(typeof(PanelsPage).GetField("WidgetLibrarySummaryText", flags));
        Assert.NotNull(typeof(PanelsPage).GetField("WidgetModifiersPanel", flags));
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
        Assert.NotNull(typeof(PanelsPage).GetMethod("QueuePosterGenerationAsync", flags));
        Assert.NotNull(typeof(PanelsPage).GetMethod("SetPanelActivationAsync", flags));
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
}
