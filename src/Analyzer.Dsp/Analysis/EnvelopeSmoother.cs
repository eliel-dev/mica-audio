namespace Analyzer.Dsp.Analysis;

public sealed class EnvelopeSmoother
{
    private readonly float rise;
    private readonly float fall;
    private readonly float motionDamping;

    private float[] targetState;
    private float[] smoothedState;

    public EnvelopeSmoother(int size, float rise, float fall, float damping = 1f)
    {
        this.rise = global::System.Math.Clamp(rise, 0f, 1f);
        this.fall = global::System.Math.Clamp(fall, 0f, 1f);
        motionDamping = global::System.Math.Clamp(damping, 0f, 1f);
        targetState = new float[size];
        smoothedState = new float[size];
    }

    public float[] Process(float[] input)
    {
        if (smoothedState.Length != input.Length)
        {
            targetState = new float[input.Length];
            smoothedState = new float[input.Length];
        }

        for (var i = 0; i < input.Length; i++)
        {
            var inputValue = input[i];

            var target = targetState[i];
            var speed = inputValue > target ? rise : fall;
            target += (inputValue - target) * speed;
            targetState[i] = target;

            var smooth = smoothedState[i];
            smooth += (target - smooth) * motionDamping;
            smoothedState[i] = smooth;
        }

        var output = new float[smoothedState.Length];
        Array.Copy(smoothedState, output, smoothedState.Length);
        return output;
    }
}
