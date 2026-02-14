using System.ComponentModel;
using System.Runtime.CompilerServices;
using MicaAudio.Core.Audio;

namespace App.WinUI.ViewModels;

internal sealed class MainPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private float sensitivityMinDb = -85f;
    private float sensitivityMaxDb = -25f;
    private float linearBoost = 1.6f;
    private int barCount = 38;
    private int fftSize = 1024;
    private float fftSmoothing = 0.8f;
    private WeightingFilter weightingFilter = WeightingFilter.Off;
    private FrequencyScale frequencyScale = FrequencyScale.Logarithmic;
    private float frequencyMinHz = 30f;
    private float frequencyMaxHz = 16_000f;
    private bool hub75PreviewEnabled;
    private string selectedRendererId = RendererIds.AudioMotionClone;
    private string currentPresetId = "audiomotion-clone";

    public float SensitivityMinDb { get => sensitivityMinDb; set => SetField(ref sensitivityMinDb, value); }
    public float SensitivityMaxDb { get => sensitivityMaxDb; set => SetField(ref sensitivityMaxDb, value); }
    public float LinearBoost { get => linearBoost; set => SetField(ref linearBoost, value); }
    public int BarCount { get => barCount; set => SetField(ref barCount, value); }
    public int FftSize { get => fftSize; set => SetField(ref fftSize, value); }
    public float FftSmoothing { get => fftSmoothing; set => SetField(ref fftSmoothing, value); }
    public WeightingFilter WeightingFilter { get => weightingFilter; set => SetField(ref weightingFilter, value); }
    public FrequencyScale FrequencyScale { get => frequencyScale; set => SetField(ref frequencyScale, value); }
    public float FrequencyMinHz { get => frequencyMinHz; set => SetField(ref frequencyMinHz, value); }
    public float FrequencyMaxHz { get => frequencyMaxHz; set => SetField(ref frequencyMaxHz, value); }
    public bool Hub75PreviewEnabled { get => hub75PreviewEnabled; set => SetField(ref hub75PreviewEnabled, value); }
    public string SelectedRendererId { get => selectedRendererId; set => SetField(ref selectedRendererId, value); }
    public string CurrentPresetId { get => currentPresetId; set => SetField(ref currentPresetId, value); }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
