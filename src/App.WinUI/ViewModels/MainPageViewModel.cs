using CommunityToolkit.Mvvm.ComponentModel;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using Visual.Win2D.Engine;

namespace App.WinUI.ViewModels;

internal sealed partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial int BarCount { get; set; }

    [ObservableProperty]
    public partial int FftSize { get; set; }

    [ObservableProperty]
    public partial float FftSmoothing { get; set; }

    [ObservableProperty]
    public partial WeightingFilter WeightingFilter { get; set; }

    [ObservableProperty]
    public partial FrequencyScale FrequencyScale { get; set; }

    [ObservableProperty]
    public partial float FrequencyMinHz { get; set; }

    [ObservableProperty]
    public partial float FrequencyMaxHz { get; set; }

    [ObservableProperty]
    public partial bool Hub75PreviewEnabled { get; set; }

    [ObservableProperty]
    public partial string SelectedRendererId { get; set; }

    [ObservableProperty]
    public partial string CurrentPresetId { get; set; }

    public MainPageViewModel()
    {
        BarCount = 38;
        FftSize = VisualizerRuntimeDefaults.DefaultFftSize;
        FftSmoothing = 0.8f;
        WeightingFilter = WeightingFilter.Off;
        FrequencyScale = FrequencyScale.Logarithmic;
        FrequencyMinHz = 30f;
        FrequencyMaxHz = 16_000f;
        SelectedRendererId = RendererIds.AudioMotionClone;
        CurrentPresetId = "audiomotion-clone";
    }
}
