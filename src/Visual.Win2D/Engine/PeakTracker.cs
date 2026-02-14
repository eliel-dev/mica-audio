namespace Visual.Win2D.Engine;

internal sealed class PeakTracker
{
    private float[] peaks = Array.Empty<float>();
    private float[] holdTimeRemaining = Array.Empty<float>();

    public float[] Update(float[] input, float deltaSeconds, float holdMs, float decayPerSecond)
    {
        EnsureSize(input.Length);

        var holdSeconds = holdMs / 1000f;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            if (value >= peaks[i])
            {
                peaks[i] = value;
                holdTimeRemaining[i] = holdSeconds;
                continue;
            }

            if (holdTimeRemaining[i] > 0f)
            {
                holdTimeRemaining[i] = Math.Max(0f, holdTimeRemaining[i] - deltaSeconds);
                continue;
            }

            peaks[i] = Math.Max(0f, peaks[i] - (decayPerSecond * deltaSeconds));
        }

        var output = new float[peaks.Length];
        Array.Copy(peaks, output, peaks.Length);
        return output;
    }

    private void EnsureSize(int size)
    {
        if (peaks.Length == size)
        {
            return;
        }

        peaks = new float[size];
        holdTimeRemaining = new float[size];
    }
}
