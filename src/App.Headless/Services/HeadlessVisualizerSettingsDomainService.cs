using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace App.Headless.Services;

internal sealed class HeadlessVisualizerSettingsDomainService
{
    private const float DefaultLinearBoost = 1.6f;
    private const int DefaultBarCount = 38;
    private const float DefaultMinHz = 20f;
    private const float DefaultMaxHz = 1000f;

    public AppSettings Migrate(AppSettings settings)
    {
        var minHz = Math.Clamp(settings.FrequencyMinHz, 16f, 10_000f);
        var maxHz = Math.Clamp(settings.FrequencyMaxHz, 250f, 20_000f);
        if (maxHz <= minHz)
        {
            minHz = DefaultMinHz;
            maxHz = DefaultMaxHz;
        }

        return new AppSettings
        {
            ActivePresetId = string.IsNullOrWhiteSpace(settings.ActivePresetId) ? "audiomotion-clone" : settings.ActivePresetId,
            SelectedRendererId = string.IsNullOrWhiteSpace(settings.SelectedRendererId) ? "audiomotion-clone" : settings.SelectedRendererId,
            Hub75PreviewEnabled = settings.Hub75PreviewEnabled,
            Brightness = Math.Clamp(settings.Brightness, 0f, 1f),
            Sensitivity = settings.Sensitivity,
            SensitivityMinDb = settings.SensitivityMinDb,
            SensitivityMaxDb = settings.SensitivityMaxDb,
            LinearBoost = CoerceLinearBoost(settings.LinearBoost),
            BarCount = settings.BarCount <= 0 ? DefaultBarCount : Math.Clamp(settings.BarCount, 8, 128),
            FrequencyScale = Enum.IsDefined(typeof(FrequencyScale), settings.FrequencyScale) ? settings.FrequencyScale : FrequencyScale.Bark,
            FrequencyMinHz = minHz,
            FrequencyMaxHz = maxHz,
            FftSize = FftSizePolicy.CoerceUiSize(settings.FftSize),
            FftSmoothing = Math.Clamp(settings.FftSmoothing, 0f, 0.99f),
            WeightingFilter = Enum.IsDefined(typeof(WeightingFilter), settings.WeightingFilter) ? settings.WeightingFilter : WeightingFilter.Off,
            DeviceFreshThresholdSeconds = settings.DeviceFreshThresholdSeconds <= 0 ? 15 : settings.DeviceFreshThresholdSeconds,
            DeviceStaleThresholdMinutes = settings.DeviceStaleThresholdMinutes <= 0 ? 2 : settings.DeviceStaleThresholdMinutes,
            DeviceDormantThresholdHours = settings.DeviceDormantThresholdHours <= 0 ? 24 : settings.DeviceDormantThresholdHours,
            AllowLegacyWebSocketQueryToken = settings.AllowLegacyWebSocketQueryToken,
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
        };
    }

    private static float CoerceLinearBoost(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultLinearBoost;
        }

        return Math.Clamp(value, 1f, 3f);
    }
}
