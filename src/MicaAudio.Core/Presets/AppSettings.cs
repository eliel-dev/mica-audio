using MicaAudio.Core.Audio;

namespace MicaAudio.Core.Presets;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#modulo-settings-presets-e-persistencia
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class AppSettings
{
    public string ActivePresetId { get; init; } = "audiomotion-clone";

    public string SelectedRendererId { get; init; } = "audiomotion-clone";

    public bool UseMicaBackdrop { get; init; } = true;

    public bool Hub75PreviewEnabled { get; init; }

    public float Brightness { get; init; } = 0.9f;

    public float Sensitivity { get; init; } = -25f;

    public float SensitivityMinDb { get; init; } = -85f;

    public float SensitivityMaxDb { get; init; } = -25f;

    public float LinearBoost { get; init; } = 1.3f;

    public int BarCount { get; init; } = 38;

    public FrequencyScale FrequencyScale { get; init; } = FrequencyScale.Bark;

    public float FrequencyMinHz { get; init; } = 20f;

    public float FrequencyMaxHz { get; init; } = 1000f;

    public int FftSize { get; init; } = 2048;

    public float FftSmoothing { get; init; } = 0.75f;

    public WeightingFilter WeightingFilter { get; init; } = WeightingFilter.B;

    public int DeviceFreshThresholdSeconds { get; init; } = 15;

    public int DeviceStaleThresholdMinutes { get; init; } = 2;

    public int DeviceDormantThresholdHours { get; init; } = 24;

    public bool AllowLegacyWebSocketQueryToken { get; init; }

    public DeviceServerMode DeviceServerMode { get; init; } = DeviceServerMode.Embedded;

    public string RemoteServerBaseAddress { get; init; } = "http://127.0.0.1:5272";

    public int WindowWidth { get; init; }

    public int WindowHeight { get; init; }
}
