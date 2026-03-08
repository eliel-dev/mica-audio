namespace App.WinUI.Services.Devices.Onboarding;

internal interface IEspToolFlashService
{
    Task<EspToolFlashResult> FlashAsync(
        string portName,
        string firmwarePath,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

internal sealed record EspToolFlashResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string Message { get; init; } = string.Empty;
}
