using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#modulo-settings-presets-e-persistencia
internal sealed class AppSettingsDomainService
{
    private const string DefaultPresetId = "audiomotion-clone";
    private const string DefaultRendererId = "audiomotion-clone";
    private const float DefaultMinDecibels = -85f;
    private const float DefaultMaxDecibels = -25f;
    private const float DefaultLinearBoost = 1.6f;

    // DOCS: docs/wiki/guides/change-visualizer-settings.md#passos
    public AppSettings Migrate(AppSettings settings)
    {
        var activePresetId = string.IsNullOrWhiteSpace(settings.ActivePresetId) ? DefaultPresetId : settings.ActivePresetId;
        var selectedRendererId = string.IsNullOrWhiteSpace(settings.SelectedRendererId)
            ? DefaultRendererId
            : settings.SelectedRendererId;

        var minHz = Math.Clamp(settings.FrequencyMinHz, 16f, 10_000f);
        var maxHz = Math.Clamp(settings.FrequencyMaxHz, 250f, 20_000f);
        if (maxHz <= minHz)
        {
            minHz = 20f;
            maxHz = 1000f;
        }

        return new AppSettings
        {
            ActivePresetId = activePresetId,
            SelectedRendererId = selectedRendererId,
            Hub75PreviewEnabled = settings.Hub75PreviewEnabled,
            Brightness = settings.Brightness,
            Sensitivity = DefaultMaxDecibels,
            SensitivityMinDb = DefaultMinDecibels,
            SensitivityMaxDb = DefaultMaxDecibels,
            LinearBoost = CoerceLinearBoost(settings.LinearBoost),
            BarCount = settings.BarCount <= 0 ? 38 : settings.BarCount,
            FrequencyScale = Enum.IsDefined(typeof(FrequencyScale), settings.FrequencyScale) ? settings.FrequencyScale : FrequencyScale.Bark,
            FrequencyMinHz = minHz,
            FrequencyMaxHz = maxHz,
            FftSize = FftSizePolicy.CoerceUiSize(settings.FftSize),
            FftSmoothing = Math.Clamp(settings.FftSmoothing, 0f, 0.99f),
            WeightingFilter = Enum.IsDefined(typeof(WeightingFilter), settings.WeightingFilter) ? settings.WeightingFilter : WeightingFilter.Off,
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
        };
    }

    public AppSettings Copy(AppSettings source, Action<AppSettingsBuilder> configure)
    {
        var builder = new AppSettingsBuilder(source);
        configure(builder);
        return builder.Build();
    }

    private static float CoerceLinearBoost(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? DefaultLinearBoost : Math.Clamp(value, 1f, 3f);

    internal sealed class AppSettingsBuilder
    {
        public AppSettingsBuilder(AppSettings source)
        {
            ActivePresetId = source.ActivePresetId;
            SelectedRendererId = source.SelectedRendererId;
            Hub75PreviewEnabled = source.Hub75PreviewEnabled;
            Brightness = source.Brightness;
            Sensitivity = DefaultMaxDecibels;
            SensitivityMinDb = DefaultMinDecibels;
            SensitivityMaxDb = DefaultMaxDecibels;
            LinearBoost = source.LinearBoost;
            BarCount = source.BarCount;
            FrequencyScale = source.FrequencyScale;
            FrequencyMinHz = source.FrequencyMinHz;
            FrequencyMaxHz = source.FrequencyMaxHz;
            FftSize = source.FftSize;
            FftSmoothing = source.FftSmoothing;
            WeightingFilter = source.WeightingFilter;
            WindowWidth = source.WindowWidth;
            WindowHeight = source.WindowHeight;
        }

        public string ActivePresetId { get; private set; }
        public string SelectedRendererId { get; private set; }
        public bool Hub75PreviewEnabled { get; private set; }
        public float Brightness { get; private set; }
        public float Sensitivity { get; private set; }
        public float SensitivityMinDb { get; private set; }
        public float SensitivityMaxDb { get; private set; }
        public float LinearBoost { get; private set; }
        public int BarCount { get; private set; }
        public FrequencyScale FrequencyScale { get; private set; }
        public float FrequencyMinHz { get; private set; }
        public float FrequencyMaxHz { get; private set; }
        public int FftSize { get; private set; }
        public float FftSmoothing { get; private set; }
        public WeightingFilter WeightingFilter { get; private set; }
        public int WindowWidth { get; private set; }
        public int WindowHeight { get; private set; }

        public void SetActivePresetId(string value) => ActivePresetId = value;
        public void SetSelectedRendererId(string value) => SelectedRendererId = value;
        public void SetHub75PreviewEnabled(bool value) => Hub75PreviewEnabled = value;
        public void SetLinearBoost(float value) => LinearBoost = value;
        public void SetBarCount(int value) => BarCount = value;
        public void SetFftSize(int value) => FftSize = value;
        public void SetFftSmoothing(float value) => FftSmoothing = value;
        public void SetWeightingFilter(WeightingFilter value) => WeightingFilter = value;
        public void SetFrequencyScale(FrequencyScale value) => FrequencyScale = value;
        public void SetFrequencyRange(float minHz, float maxHz)
        {
            FrequencyMinHz = minHz;
            FrequencyMaxHz = maxHz;
        }

        public void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        public AppSettings Build() => new()
        {
            ActivePresetId = ActivePresetId,
            SelectedRendererId = SelectedRendererId,
            Hub75PreviewEnabled = Hub75PreviewEnabled,
            Brightness = Brightness,
            Sensitivity = Sensitivity,
            SensitivityMinDb = SensitivityMinDb,
            SensitivityMaxDb = SensitivityMaxDb,
            LinearBoost = LinearBoost,
            BarCount = BarCount,
            FrequencyScale = FrequencyScale,
            FrequencyMinHz = FrequencyMinHz,
            FrequencyMaxHz = FrequencyMaxHz,
            FftSize = FftSize,
            FftSmoothing = FftSmoothing,
            WeightingFilter = WeightingFilter,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
        };
    }
}
