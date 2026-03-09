using App.WinUI.Services.Gif;
using Microsoft.UI.Xaml;

namespace App.WinUI.Views;

public partial class MainPage
{
    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-menu-de-configuracao-fluent-2
    private const string DefaultRendererSelectionHint = "Escolhe como o visualizador é desenhado no canvas principal.";
    private const string FixedRendererSelectionHint = "O preset AudioMotion Clone usa o renderizador nativo e mantém esta opção fixa.";

    internal static bool ShouldShowContentModeSetting(GifContentSourceMode contentMode, int loadedGifFrameCount)
        => contentMode == GifContentSourceMode.Gif || loadedGifFrameCount > 0;

    internal static string ResolveRendererSelectionHint(bool usesFixedRenderer)
        => usesFixedRenderer
            ? FixedRendererSelectionHint
            : DefaultRendererSelectionHint;

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (fullscreen)
        {
            SettingsButton.IsChecked = false;
            return;
        }

        SetSettingsPaneOpen(!SettingsSplitView.IsPaneOpen);
    }

    private void OnSettingsPaneCloseClicked(object sender, RoutedEventArgs e)
        => SetSettingsPaneOpen(false);

    private void SetSettingsPaneOpen(bool isOpen)
    {
        SettingsSplitView.IsPaneOpen = !fullscreen && isOpen;
        SyncSettingsButtonState();
    }

    private void EnsureVisibleUiState()
    {
        App.SetShellChromeHidden(false);
        fullscreen = false;
        ControlsPanel.Visibility = Visibility.Visible;
        SetSettingsPaneOpen(false);
        HideFullscreenButtonOverlay();
        HidePresetSwitchHud();
    }

    private void SyncSettingsButtonState()
    {
        if (SettingsButton is not null)
        {
            SettingsButton.IsChecked = SettingsSplitView.IsPaneOpen;
        }
    }

    private void UpdateSettingsPaneVisualState()
    {
        UpdateSettingsPaneHeader();
        UpdateSettingsPaneModeState();
        SyncSettingsButtonState();
    }

    private void UpdateSettingsPaneHeader()
    {
        if (CurrentPresetNameText is null)
        {
            return;
        }

        CurrentPresetNameText.Text = string.IsNullOrWhiteSpace(activePreset.Name)
            ? "Preset indisponível"
            : activePreset.Name;
    }

    private void UpdateSettingsPaneModeState()
    {
        if (ContentModeSettingRow is null)
        {
            return;
        }

        ContentModeSettingRow.Visibility = ShouldShowContentModeSetting(contentSourceMode, gifFrames.Count)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
