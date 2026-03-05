namespace App.Headless.Services;

internal enum HeadlessOnboardingStage
{
    Idle,
    SelectPort,
    Flashing,
    Pairing,
    Done,
    Failed,
}

internal sealed record HeadlessOnboardingProgress
{
    public required HeadlessOnboardingStage Stage { get; init; }
    public required string Message { get; init; }
    public int Percent { get; init; }
}

internal sealed record HeadlessOnboardingResult
{
    public bool Success { get; init; }
    public string? PairCode { get; init; }
    public string? ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
}

internal sealed record HeadlessOnboardingStatus
{
    public bool InProgress { get; init; }
    public HeadlessOnboardingStage Stage { get; init; } = HeadlessOnboardingStage.Idle;
    public string Message { get; init; } = string.Empty;
    public int Percent { get; init; }
    public string? PortName { get; init; }
    public bool Completed { get; init; }
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? PairCode { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record HeadlessSerialPortDescriptor
{
    public required string PortName { get; init; }
    public required string DisplayName { get; init; }
    public string? PnpDeviceId { get; init; }
    public string? VidPid { get; init; }
    public bool IsPreferredDevice { get; init; }
    public int PriorityRank { get; init; }
    public string Label => string.IsNullOrWhiteSpace(DisplayName)
        ? PortName
        : $"{PortName} - {DisplayName}";
}
