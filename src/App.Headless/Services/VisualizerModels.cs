using MicaAudio.Core.Audio;

namespace App.Headless.Services;

internal sealed record VisualizerSpectrumSnapshot
{
    public IReadOnlyList<float> Bands { get; init; } = Array.Empty<float>();
    public float Level { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record VisualizerSettingsSnapshot
{
    public float LinearBoost { get; init; }
    public int BarCount { get; init; }
    public int FftSize { get; init; }
    public float FftSmoothing { get; init; }
    public WeightingFilter WeightingFilter { get; init; }
    public FrequencyScale FrequencyScale { get; init; }
    public float FrequencyMinHz { get; init; }
    public float FrequencyMaxHz { get; init; }
    public bool Hub75Enabled { get; init; }
    public float Brightness { get; init; }
}

internal sealed record VisualizerGifStateSnapshot
{
    public string? SourceUrl { get; init; }
    public string ScaleMode { get; init; } = "fit";
    public bool IsPlaying { get; init; }
    public int FrameCount { get; init; }
    public string? Error { get; init; }
}

internal sealed record VisualizerRuntimeStateSnapshot
{
    public bool AudioCapturing { get; init; }
    public int TargetFps { get; init; }
    public string Status { get; init; } = "Parado";
    public string ContentMode { get; init; } = "audio";
    public VisualizerSettingsSnapshot Settings { get; init; } = new();
    public VisualizerGifStateSnapshot Gif { get; init; } = new();
    public VisualizerSpectrumSnapshot Spectrum { get; init; } = new();
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record VisualizerSettingsUpdate
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
