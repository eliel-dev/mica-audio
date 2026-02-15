namespace App.WinUI.Services.Devices;

internal sealed class FirmwareBuildRequest
{
    public string Profile { get; init; } = "matrixportal_s3_stable";

    public string WorkingDirectory { get; init; } = string.Empty;
}
