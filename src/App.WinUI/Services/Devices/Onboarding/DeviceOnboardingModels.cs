namespace App.WinUI.Services.Devices.Onboarding;

internal enum DeviceOnboardingStage
{
    Idle,
    InputWifi,
    SelectPort,
    Flashing,
    Provisioning,
    Pairing,
    Verifying,
    Done,
    Failed,
}

internal sealed record DeviceOnboardingRequest
{
    public required string PortName { get; init; }
}

internal sealed record DeviceOnboardingProgress
{
    public required DeviceOnboardingStage Stage { get; init; }

    public required string Message { get; init; }

    public int Percent { get; init; }
}

internal sealed record DeviceOnboardingResult
{
    public bool Success { get; init; }

    public string? DeviceId { get; init; }

    public string? ErrorCode { get; init; }

    public string? PairCode { get; init; }

    public string Message { get; init; } = string.Empty;
}
