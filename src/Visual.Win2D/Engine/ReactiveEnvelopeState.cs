namespace Visual.Win2D.Engine;

public static class ReactiveEnvelopeState
{
    public const float AttackRate = 12f;
    public const float ReleaseRate = 7f;

    public static float Blend(float current, float target, float deltaSeconds)
    {
        var clampedDelta = Math.Clamp(deltaSeconds, 0.001f, 0.5f);
        var rate = target >= current ? AttackRate : ReleaseRate;
        var factor = 1f - MathF.Exp(-rate * clampedDelta);
        return current + ((target - current) * factor);
    }
}
