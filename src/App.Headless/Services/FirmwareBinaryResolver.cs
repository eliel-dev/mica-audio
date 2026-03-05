namespace App.Headless.Services;

internal interface IFirmwareBinaryResolver
{
    bool TryResolve(out string firmwarePath, out string error);
}

internal sealed class FirmwareBinaryResolver : IFirmwareBinaryResolver
{
    private const string FirmwareFileName = "esp32s3-devkitc1-128x64-dma_exp_merged.bin";

    public bool TryResolve(out string firmwarePath, out string error)
    {
        firmwarePath = string.Empty;
        error = string.Empty;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware", FirmwareFileName),
            Path.Combine(Environment.CurrentDirectory, "AppData", "Firmware", FirmwareFileName),
            Path.Combine(Environment.CurrentDirectory, "src", "App.WinUI", "AppData", "Firmware", FirmwareFileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App.WinUI", "AppData", "Firmware", FirmwareFileName)),
        };

        foreach (var candidate in candidates
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                firmwarePath = candidate;
                return true;
            }
        }

        error = $"Arquivo de firmware nao encontrado ({FirmwareFileName}).";
        return false;
    }
}
