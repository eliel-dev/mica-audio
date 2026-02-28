using CommunityToolkit.Mvvm.ComponentModel;
using MicaAudio.Core.Audio;
using Visual.Win2D.Engine;

namespace App.WinUI.ViewModels;

internal sealed partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    private float linearBoost = 1.6f;

    [ObservableProperty]
    private int barCount = 38;

    [ObservableProperty]
    private int fftSize = 1024;

    [ObservableProperty]
    private float fftSmoothing = 0.8f;

    [ObservableProperty]
    private WeightingFilter weightingFilter = WeightingFilter.Off;

    [ObservableProperty]
    private FrequencyScale frequencyScale = FrequencyScale.Logarithmic;

    [ObservableProperty]
    private float frequencyMinHz = 30f;

    [ObservableProperty]
    private float frequencyMaxHz = 16_000f;

    [ObservableProperty]
    private bool hub75PreviewEnabled;

    [ObservableProperty]
    private string selectedRendererId = RendererIds.AudioMotionClone;

    [ObservableProperty]
    private string currentPresetId = "audiomotion-clone";
}
