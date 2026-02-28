using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#galeria-de-presets
internal static class PresetPreviewSignalFactory
{
    public const int SampleRate = 48_000;
    public const int HopSize = 256;
    private const int PartialCount = 8;

    public static PcmFrame CreatePcmFrame(
        PresetDefinition preset,
        PresetPreviewSettingsSnapshot settings,
        float elapsedSeconds,
        int sampleCount,
        int sampleRate)
    {
        var count = Math.Max(1, sampleCount);
        var rate = Math.Max(1, sampleRate);
        var samples = new float[count];
        var seed = ComputePresetSeed(preset.PresetId);
        var nyquist = rate * 0.5f;
        var minFrequency = Math.Clamp(settings.FrequencyMinHz, 20f, Math.Max(20f, nyquist * 0.45f));
        var maxFrequency = Math.Clamp(settings.FrequencyMaxHz, minFrequency + 10f, Math.Max(minFrequency + 10f, nyquist * 0.45f));
        var minLog = MathF.Log(MathF.Max(1f, minFrequency));
        var maxLog = MathF.Log(MathF.Max(minFrequency + 1f, maxFrequency));
        var partialWeight = 1f / PartialCount;

        for (var i = 0; i < count; i++)
        {
            var t = elapsedSeconds + (i / (float)rate);
            var pulse = 0.62f + (0.38f * MathF.Sin((MathF.Tau * (0.9f + (seed * 0.2f)) * t) + (seed * 0.5f)));
            var shimmer = 0f;

            for (var partial = 0; partial < PartialCount; partial++)
            {
                var ratio = PartialCount == 1 ? 0f : partial / (float)(PartialCount - 1);
                var frequency = MathF.Exp(minLog + ((maxLog - minLog) * ratio));
                var sweep = 0.6f + (0.4f * MathF.Sin((MathF.Tau * (0.42f + (ratio * 0.23f) + (seed * 0.08f)) * t) + (ratio * 3.7f) + (seed * 6.1f)));
                var phase = (seed * MathF.Tau * (0.25f + (ratio * 0.5f))) + (ratio * MathF.PI * 0.75f);
                shimmer += MathF.Sin((MathF.Tau * frequency * t) + phase) * sweep * partialWeight;
            }

            var bass = 0.25f * MathF.Sin(MathF.Tau * MathF.Max(32f, minFrequency * 0.85f) * t);
            var transient = 0.12f * MathF.Sin((MathF.Tau * MathF.Min(maxFrequency, minFrequency * 6f) * t) + (seed * 4.2f));
            var sample = ((shimmer * 0.78f) + bass + transient) * pulse;
            samples[i] = Math.Clamp(sample, -1f, 1f);
        }

        return new PcmFrame(samples, 0L);
    }

    private static float ComputePresetSeed(string presetId)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in presetId)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return (hash % 10000) / 10000f;
        }
    }
}
