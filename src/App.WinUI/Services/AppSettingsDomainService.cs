using System.Diagnostics.CodeAnalysis;
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
    private const int DefaultDeviceFreshThresholdSeconds = 15;
    private const int DefaultDeviceStaleThresholdMinutes = 2;
    private const int DefaultDeviceDormantThresholdHours = 24;

    // DOCS: docs/wiki/guides/change-visualizer-settings.md#passos
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como metodo de instancia para preservar o contrato do service usado por DI e acomodar evolucao stateful futura.")]
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

        var thresholds = NormalizeDeviceLifecycleThresholds(
            settings.DeviceFreshThresholdSeconds,
            settings.DeviceStaleThresholdMinutes,
            settings.DeviceDormantThresholdHours);

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
            FrequencyScale = Enum.IsDefined(settings.FrequencyScale) ? settings.FrequencyScale : FrequencyScale.Bark,
            FrequencyMinHz = minHz,
            FrequencyMaxHz = maxHz,
            FftSize = FftSizePolicy.CoerceUiSize(settings.FftSize),
            FftSmoothing = Math.Clamp(settings.FftSmoothing, 0f, 0.99f),
            WeightingFilter = Enum.IsDefined(settings.WeightingFilter) ? settings.WeightingFilter : WeightingFilter.Off,
            DeviceFreshThresholdSeconds = thresholds.DeviceFreshThresholdSeconds,
            DeviceStaleThresholdMinutes = thresholds.DeviceStaleThresholdMinutes,
            DeviceDormantThresholdHours = thresholds.DeviceDormantThresholdHours,
            AllowLegacyWebSocketQueryToken = settings.AllowLegacyWebSocketQueryToken,
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
        };
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como metodo de instancia para preservar o contrato do service usado por DI e acomodar evolucao stateful futura.")]
    public AppSettings Copy(AppSettings source, Action<AppSettingsBuilder> configure)
    {
        var builder = new AppSettingsBuilder(source);
        configure(builder);
        return builder.Build();
    }

    private static float CoerceLinearBoost(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? DefaultLinearBoost : Math.Clamp(value, 1f, 3f);

    private static DeviceLifecycleThresholdValues NormalizeDeviceLifecycleThresholds(
        int freshThresholdSeconds,
        int staleThresholdMinutes,
        int dormantThresholdHours)
    {
        var normalizedFresh = freshThresholdSeconds <= 0
            ? DefaultDeviceFreshThresholdSeconds
            : Math.Clamp(freshThresholdSeconds, 5, 120);
        var normalizedStale = staleThresholdMinutes <= 0
            ? DefaultDeviceStaleThresholdMinutes
            : Math.Clamp(staleThresholdMinutes, 1, 240);
        var normalizedDormant = dormantThresholdHours <= 0
            ? DefaultDeviceDormantThresholdHours
            : Math.Clamp(dormantThresholdHours, 1, 720);

        var fresh = TimeSpan.FromSeconds(normalizedFresh);
        var stale = TimeSpan.FromMinutes(normalizedStale);
        var dormant = TimeSpan.FromHours(normalizedDormant);

        if (fresh >= stale)
        {
            stale = fresh + TimeSpan.FromMinutes(1);
            normalizedStale = Math.Clamp((int)Math.Ceiling(stale.TotalMinutes), 1, 240);
            stale = TimeSpan.FromMinutes(normalizedStale);
        }

        if (stale >= dormant)
        {
            dormant = stale + TimeSpan.FromHours(1);
            normalizedDormant = Math.Clamp((int)Math.Ceiling(dormant.TotalHours), 1, 720);
        }

        return new DeviceLifecycleThresholdValues(normalizedFresh, normalizedStale, normalizedDormant);
    }

    private readonly record struct DeviceLifecycleThresholdValues(
        int DeviceFreshThresholdSeconds,
        int DeviceStaleThresholdMinutes,
        int DeviceDormantThresholdHours);

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
            DeviceFreshThresholdSeconds = source.DeviceFreshThresholdSeconds;
            DeviceStaleThresholdMinutes = source.DeviceStaleThresholdMinutes;
            DeviceDormantThresholdHours = source.DeviceDormantThresholdHours;
            AllowLegacyWebSocketQueryToken = source.AllowLegacyWebSocketQueryToken;
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
        public int DeviceFreshThresholdSeconds { get; private set; }
        public int DeviceStaleThresholdMinutes { get; private set; }
        public int DeviceDormantThresholdHours { get; private set; }
        public bool AllowLegacyWebSocketQueryToken { get; private set; }
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
        public void SetDeviceFreshThresholdSeconds(int value) => DeviceFreshThresholdSeconds = value;
        public void SetDeviceStaleThresholdMinutes(int value) => DeviceStaleThresholdMinutes = value;
        public void SetDeviceDormantThresholdHours(int value) => DeviceDormantThresholdHours = value;
        public void SetAllowLegacyWebSocketQueryToken(bool value) => AllowLegacyWebSocketQueryToken = value;
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

        public AppSettings Build()
        {
            var thresholds = NormalizeDeviceLifecycleThresholds(
                DeviceFreshThresholdSeconds,
                DeviceStaleThresholdMinutes,
                DeviceDormantThresholdHours);

            return new AppSettings
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
                DeviceFreshThresholdSeconds = thresholds.DeviceFreshThresholdSeconds,
                DeviceStaleThresholdMinutes = thresholds.DeviceStaleThresholdMinutes,
                DeviceDormantThresholdHours = thresholds.DeviceDormantThresholdHours,
                AllowLegacyWebSocketQueryToken = AllowLegacyWebSocketQueryToken,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
            };
        }
    }
}
