using System.Diagnostics.CodeAnalysis;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#modulo-settings-presets-e-persistencia
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
internal sealed class AppSettingsDomainService
{
    // DOCS: docs/wiki/guides/change-visualizer-settings.md#passos
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como metodo de instancia para preservar o contrato do service usado por DI e acomodar evolucao stateful futura.")]
    public AppSettings Migrate(AppSettings settings) => Normalize(settings);

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como metodo de instancia para preservar o contrato do service usado por DI e acomodar evolucao stateful futura.")]
    public VisualizerRuntimeSettings GetVisualizerRuntimeSettings(AppSettings settings)
    {
        return VisualizerRuntimeSettings.From(settings);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como metodo de instancia para preservar o contrato do service usado por DI e acomodar evolucao stateful futura.")]
    public AppSettings Copy(AppSettings source, Action<AppSettingsBuilder> configure)
    {
        var builder = new AppSettingsBuilder(source);
        configure(builder);
        return builder.Build();
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var visualizer = VisualizerRuntimeSettings.From(settings);
        var thresholds = DeviceLifecycleSettings.From(settings);

        return new AppSettings
        {
            ActivePresetId = visualizer.ActivePresetId,
            SelectedRendererId = visualizer.SelectedRendererId,
            UseMicaBackdrop = settings.UseMicaBackdrop,
            Hub75PreviewEnabled = settings.Hub75PreviewEnabled,
            Brightness = settings.Brightness,
            Sensitivity = VisualizerRuntimeDefaults.DefaultMaxDecibels,
            SensitivityMinDb = VisualizerRuntimeDefaults.DefaultMinDecibels,
            SensitivityMaxDb = VisualizerRuntimeDefaults.DefaultMaxDecibels,
            LinearBoost = visualizer.LinearBoost,
            BarCount = visualizer.DisplayBandCount,
            FrequencyScale = visualizer.FrequencyScale,
            FrequencyMinHz = visualizer.FrequencyMinHz,
            FrequencyMaxHz = visualizer.FrequencyMaxHz,
            FftSize = visualizer.FftSize,
            FftSmoothing = visualizer.FftSmoothing,
            WeightingFilter = visualizer.WeightingFilter,
            DeviceFreshThresholdSeconds = thresholds.DeviceFreshThresholdSeconds,
            DeviceStaleThresholdMinutes = thresholds.DeviceStaleThresholdMinutes,
            DeviceDormantThresholdHours = thresholds.DeviceDormantThresholdHours,
            AllowLegacyWebSocketQueryToken = settings.AllowLegacyWebSocketQueryToken,
            RemoteServerBaseAddress = NormalizeRemoteServerBaseAddress(settings.RemoteServerBaseAddress),
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
        };
    }

    internal sealed class AppSettingsBuilder
    {
        public AppSettingsBuilder(AppSettings source)
        {
            var visualizer = VisualizerRuntimeSettings.From(source);

            ActivePresetId = visualizer.ActivePresetId;
            SelectedRendererId = visualizer.SelectedRendererId;
            UseMicaBackdrop = source.UseMicaBackdrop;
            Hub75PreviewEnabled = source.Hub75PreviewEnabled;
            Brightness = source.Brightness;
            Sensitivity = VisualizerRuntimeDefaults.DefaultMaxDecibels;
            SensitivityMinDb = VisualizerRuntimeDefaults.DefaultMinDecibels;
            SensitivityMaxDb = VisualizerRuntimeDefaults.DefaultMaxDecibels;
            LinearBoost = visualizer.LinearBoost;
            BarCount = visualizer.DisplayBandCount;
            FrequencyScale = visualizer.FrequencyScale;
            FrequencyMinHz = visualizer.FrequencyMinHz;
            FrequencyMaxHz = visualizer.FrequencyMaxHz;
            FftSize = visualizer.FftSize;
            FftSmoothing = visualizer.FftSmoothing;
            WeightingFilter = visualizer.WeightingFilter;
            DeviceFreshThresholdSeconds = source.DeviceFreshThresholdSeconds;
            DeviceStaleThresholdMinutes = source.DeviceStaleThresholdMinutes;
            DeviceDormantThresholdHours = source.DeviceDormantThresholdHours;
            AllowLegacyWebSocketQueryToken = source.AllowLegacyWebSocketQueryToken;
            RemoteServerBaseAddress = NormalizeRemoteServerBaseAddress(source.RemoteServerBaseAddress);
            WindowWidth = source.WindowWidth;
            WindowHeight = source.WindowHeight;
        }

        public string ActivePresetId { get; private set; }
        public string SelectedRendererId { get; private set; }
        public bool UseMicaBackdrop { get; private set; }
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
        public string RemoteServerBaseAddress { get; private set; }
        public int WindowWidth { get; private set; }
        public int WindowHeight { get; private set; }

        public void SetActivePresetId(string value) => ActivePresetId = value;
        public void SetSelectedRendererId(string value) => SelectedRendererId = value;
        public void SetUseMicaBackdrop(bool value) => UseMicaBackdrop = value;
        public void SetHub75PreviewEnabled(bool value) => Hub75PreviewEnabled = value;
        public void SetBarCount(int value) => BarCount = value;
        public void SetFftSize(int value) => FftSize = VisualizerRuntimeSettings.NormalizeFftSize(value);
        public void SetFftSmoothing(float value) => FftSmoothing = value;
        public void SetWeightingFilter(WeightingFilter value) => WeightingFilter = value;
        public void SetDeviceFreshThresholdSeconds(int value) => DeviceFreshThresholdSeconds = value;
        public void SetDeviceStaleThresholdMinutes(int value) => DeviceStaleThresholdMinutes = value;
        public void SetDeviceDormantThresholdHours(int value) => DeviceDormantThresholdHours = value;
        public void SetAllowLegacyWebSocketQueryToken(bool value) => AllowLegacyWebSocketQueryToken = value;
        public void SetRemoteServerBaseAddress(string value) => RemoteServerBaseAddress = NormalizeRemoteServerBaseAddress(value);
        public void SetFrequencyScale(FrequencyScale value) => FrequencyScale = value;
        public void SetFrequencyRange(float minHz, float maxHz)
        {
            FrequencyMinHz = minHz;
            FrequencyMaxHz = maxHz;
        }

        public void SetVisualizerRuntimeSettings(VisualizerRuntimeSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            ActivePresetId = settings.ActivePresetId;
            SelectedRendererId = settings.SelectedRendererId;
            LinearBoost = settings.LinearBoost;
            BarCount = settings.DisplayBandCount;
            FrequencyScale = settings.FrequencyScale;
            FrequencyMinHz = settings.FrequencyMinHz;
            FrequencyMaxHz = settings.FrequencyMaxHz;
            FftSize = settings.FftSize;
            FftSmoothing = settings.FftSmoothing;
            WeightingFilter = settings.WeightingFilter;
        }

        public void SetWindowSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }

        public AppSettings Build()
        {
            return Normalize(new AppSettings
            {
                ActivePresetId = ActivePresetId,
                SelectedRendererId = SelectedRendererId,
                UseMicaBackdrop = UseMicaBackdrop,
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
                DeviceFreshThresholdSeconds = DeviceFreshThresholdSeconds,
                DeviceStaleThresholdMinutes = DeviceStaleThresholdMinutes,
                DeviceDormantThresholdHours = DeviceDormantThresholdHours,
                AllowLegacyWebSocketQueryToken = AllowLegacyWebSocketQueryToken,
                RemoteServerBaseAddress = RemoteServerBaseAddress,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
            });
        }
    }

    private static string NormalizeRemoteServerBaseAddress(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "http://127.0.0.1:5272";
        }

        return uri.ToString().TrimEnd('/');
    }
}
