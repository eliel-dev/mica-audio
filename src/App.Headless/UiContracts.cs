using Device.Protocol.Models;
using App.Headless.Services;

namespace App.Headless;

internal sealed record UiAppCatalogItem(string Id, string Name);

internal sealed record UiHealthResponse
{
    public string Status { get; init; } = "ok";
    public bool AudioCapturing { get; init; }
    public int DevicesOnline { get; init; }
    public bool CommandInProgress { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
}

internal sealed record UiCommandStatusViewModel
{
    public bool InProgress { get; init; }
    public int Percent { get; init; }
    public string Status { get; init; } = "Comandos: pronto";
    public string? Stage { get; init; }
    public string? DeviceId { get; init; }
    public string? CommandId { get; init; }
    public string? ErrorCode { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

internal sealed record UiDeviceListItem
{
    public string DeviceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Profile { get; init; } = "dma_exp";
    public string Status { get; init; } = "offline";
    public bool IsRegistered { get; init; }
    public DateTimeOffset LastSeenUtc { get; init; }
    public DateTimeOffset? FirstSeenUtc { get; init; }
    public DateTimeOffset? LastTelemetryUtc { get; init; }
    public DateTimeOffset? LastAuthUtc { get; init; }
    public string ConfigState { get; init; } = "unknown";
    public string? LastKnownIp { get; init; }
    public int? LastKnownRssi { get; init; }
    public int? UptimeSeconds { get; init; }
    public int? LoopLoadPercent { get; init; }
    public long? FreeHeapBytes { get; init; }
    public long? LargestHeapBlockBytes { get; init; }
    public bool? PsramAvailable { get; init; }
    public long? FreePsramBytes { get; init; }
    public long? LargestPsramBlockBytes { get; init; }
    public bool? WifiConnected { get; init; }
    public string? WifiState { get; init; }
    public bool? ProvisioningPortalActive { get; init; }
    public bool? AuxLedAvailable { get; init; }
    public bool? TestLedAvailable { get; init; }
    public string? LastWifiEvent { get; init; }
    public uint? StreamLastSequence { get; init; }
    public uint? StreamFramesReceived { get; init; }
    public uint? StreamFramesApplied { get; init; }
    public uint? StreamSequenceGapCount { get; init; }
    public uint? StreamInvalidFrameCount { get; init; }
    public string? FirmwareVersion { get; init; }
    public uint? TelemetrySequence { get; init; }
    public int? BrightnessCap { get; init; }
    public int? BrightnessRequested { get; init; }
    public int? BrightnessApplied { get; init; }
    public bool? TestLedEnabled { get; init; }
    public int? TestLedDuty { get; init; }
    public string? ActiveAppId { get; init; }
    public string? ActiveAppName { get; init; }
    public string? BoardModel { get; init; }
    public string? PanelType { get; init; }

    public string StatusLine { get; init; } = string.Empty;
    public string RegistrationLine { get; init; } = string.Empty;
    public string AppLabel { get; init; } = string.Empty;
    public string SignalLabel { get; init; } = string.Empty;
    public string LifecyclePrimaryState { get; init; } = string.Empty;
    public string LifecycleSecondaryState { get; init; } = string.Empty;
    public string LifecycleLastSeenLabel { get; init; } = string.Empty;
    public bool CanRunCommands { get; init; }
}

internal sealed record UiMetricsViewModel
{
    public string StatusLabel { get; init; } = "Sem metricas";
    public string UptimeLabel { get; init; } = "Uptime: -";
    public string HeapLabel { get; init; } = "Heap livre: -";
    public string PsramLabel { get; init; } = "PSRAM: -";
    public string NetworkLabel { get; init; } = "Wi-Fi: -";
    public int? LoopLoadPercent { get; init; }
    public double LoopLoadProgress { get; init; }
    public double? HeapFragmentationProgress { get; init; }
    public double? PsramFragmentationProgress { get; init; }
    public bool HasMetrics { get; init; }
    public bool IsOfflineSnapshot { get; init; }
    public bool IsPsramAvailable { get; init; }
    public string PlaceholderMessage { get; init; } = string.Empty;
}

internal sealed record UiConnectivityViewModel
{
    public string WifiStateLabel { get; init; } = "Rede Wi-Fi: -";
    public string PortalStateLabel { get; init; } = "Modo configuracao: -";
    public string UptimeLabel { get; init; } = "Ligado ha: -";
    public string LastEventLabel { get; init; } = "Ultimo evento de rede: -";
    public string AuxLedLabel { get; init; } = "LED auxiliar: -";
}

internal sealed record UiEspDashViewModel
{
    public string UptimeValue { get; init; } = "-";
    public string FpsValue { get; init; } = "-";
    public string HeapValue { get; init; } = "-";
    public string HeapSubValue { get; init; } = "Fragmentacao -";
    public int CpuPercent { get; init; }
    public string SuccessRate { get; init; } = "-";
    public uint StreamFramesReceived { get; init; }
    public uint StreamFramesApplied { get; init; }
    public uint StreamGapCount { get; init; }
    public uint StreamInvalidCount { get; init; }
    public uint StreamLastSequence { get; init; }
    public IReadOnlyList<int> LoopSeries { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> HeapSeries { get; init; } = Array.Empty<int>();
}

internal sealed record UiDeviceDetailsViewModel
{
    public string DeviceId { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public string Name { get; init; } = "Nenhum dispositivo selecionado";
    public string Subtitle { get; init; } = "-";
    public string RegistrationLine { get; init; } = "-";
    public string AppLabel { get; init; } = "App ativo: -";
    public string ServerLine { get; init; } = "Servidor: iniciando...";
    public string SignalLabel { get; init; } = "Sinal -";
    public bool CanRunCommands { get; init; }
    public bool CanRemove { get; init; }
    public bool TestLedAvailable { get; init; }
    public int BrightnessMin { get; init; } = 30;
    public int BrightnessMax { get; init; } = 160;
    public int BrightnessValue { get; init; } = 160;
    public string BrightnessValueLabel { get; init; } = "160/160";
    public string BrightnessStatusLabel { get; init; } = "Brilho atual no painel: -";
    public string HeartbeatLabel { get; init; } = "Sinal de vida: - | LED de teste: - | Intensidade do LED: -";
    public IReadOnlyList<int> LoopTrendSeries { get; init; } = Array.Empty<int>();
    public string LoopTrendCaption { get; init; } = "Historico de uso do processador";
    public string LoopTrendPlaceholder { get; init; } = "Sem historico de CPU ainda.";
    public UiMetricsViewModel Metrics { get; init; } = new();
    public UiEspDashViewModel EspDash { get; init; } = new();
    public UiConnectivityViewModel Connectivity { get; init; } = new();
    public UiCommandStatusViewModel Command { get; init; } = new();
    public int DeviceLogCount { get; init; }
    public string DeviceLogsPlaceholder { get; init; } = string.Empty;
}

internal sealed record UiDeviceLogsViewModel
{
    public string DeviceId { get; init; } = string.Empty;
    public IReadOnlyList<string> DeviceEntries { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GlobalEntries { get; init; } = Array.Empty<string>();
    public string Placeholder { get; init; } = string.Empty;
}

internal sealed record UiOnboardingPortViewModel
{
    public string PortName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? PnpDeviceId { get; init; }
    public string? VidPid { get; init; }
    public bool IsPreferredDevice { get; init; }
    public int PriorityRank { get; init; }
    public string Label { get; init; } = string.Empty;
}

internal sealed record UiOnboardingStatusViewModel
{
    public bool InProgress { get; init; }
    public string Stage { get; init; } = "idle";
    public string Message { get; init; } = string.Empty;
    public int Percent { get; init; }
    public string? PortName { get; init; }
    public bool Completed { get; init; }
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? PairCode { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

internal sealed record UiOnboardingStartRequest(string PortName);

internal sealed record UiOnboardingStartResponse
{
    public bool Accepted { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Message { get; init; }
    public UiOnboardingStatusViewModel Snapshot { get; init; } = new();
}

internal sealed record UiBrightnessRequest(int Value);

internal sealed record UiAppChangeRequest(string AppId);

internal sealed record UiCommandResponse
{
    public string DeviceId { get; init; } = string.Empty;
    public bool Accepted { get; init; }
    public bool Completed { get; init; }
    public bool Success { get; init; }
    public string? CommandId { get; init; }
    public string? Stage { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
}

internal sealed record UiRemoveDeviceResponse
{
    public string DeviceId { get; init; } = string.Empty;
    public bool Removed { get; init; }
    public bool WasOnline { get; init; }
    public bool RevokeAttempted { get; init; }
    public bool RevokeSucceeded { get; init; }
    public string? RevokeError { get; init; }
    public string Message { get; init; } = string.Empty;
}

internal sealed record UiDeviceCommandRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public DeviceCommandType CommandType { get; init; }
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
    public string OperationLabel { get; init; } = "comando";
}

internal sealed record UiVisualizerStateResponse
{
    public bool AudioCapturing { get; init; }
    public int TargetFps { get; init; }
    public string Status { get; init; } = "Parado";
    public string ContentMode { get; init; } = "audio";
    public VisualizerSettingsSnapshot Settings { get; init; } = new();
    public VisualizerGifStateSnapshot Gif { get; init; } = new();
    public VisualizerSpectrumSnapshot Spectrum { get; init; } = new();
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

internal sealed record UiVisualizerSettingsRequest
{
    public float? LinearBoost { get; init; }
    public int? BarCount { get; init; }
    public int? FftSize { get; init; }
    public float? FftSmoothing { get; init; }
    public string? WeightingFilter { get; init; }
    public string? FrequencyScale { get; init; }
    public float? FrequencyMinHz { get; init; }
    public float? FrequencyMaxHz { get; init; }
    public bool? Hub75Enabled { get; init; }
    public float? Brightness { get; init; }
}

internal sealed record UiVisualizerHub75Request(bool Enabled);

internal sealed record UiVisualizerContentModeRequest(string Mode);

internal sealed record UiVisualizerGifUrlRequest(string Url);
