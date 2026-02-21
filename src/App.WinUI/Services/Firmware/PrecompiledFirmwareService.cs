namespace App.WinUI.Services.Firmware;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
internal sealed class PrecompiledFirmwareService
{
    private static readonly IReadOnlyList<PrecompiledFirmwareOption> Options =
    [
        new PrecompiledFirmwareOption
        {
            Id = "stable",
            DisplayName = "Firmware Stable",
            Description = "Perfil estavel (Protomatter).",
            FileName = "matrixportal-s3-stable_merged.bin",
        },
        new PrecompiledFirmwareOption
        {
            Id = "dma_exp",
            DisplayName = "Firmware DMA Experimental",
            Description = "Perfil experimental com DMA.",
            FileName = "matrixportal-s3-dma_exp_merged.bin",
        },
    ];

    public event EventHandler<string>? LogMessage;

    public IReadOnlyList<PrecompiledFirmwareOption> GetOptions() => Options;

    // DOCS: docs/wiki/guides/build-export-firmware.md#passos
    public bool TryResolveSource(string optionId, out string sourcePath, out string error)
    {
        sourcePath = string.Empty;
        error = string.Empty;

        var option = Options.FirstOrDefault(item => string.Equals(item.Id, optionId, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            error = $"Opcao de firmware invalida: {optionId}";
            return false;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware", option.FileName),
            Path.Combine(Environment.CurrentDirectory, "AppData", "Firmware", option.FileName),
            Path.Combine(Environment.CurrentDirectory, "src", "App.WinUI", "AppData", "Firmware", option.FileName),
        }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                sourcePath = candidate;
                return true;
            }
        }

        error = $"Arquivo de firmware nao encontrado para '{option.DisplayName}' ({option.FileName}).";
        return false;
    }

    public async Task CopyToAsync(string optionId, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destino invalido.", nameof(destinationPath));
        }

        if (!TryResolveSource(optionId, out var sourcePath, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException("Nao foi possivel resolver a pasta de destino.");
        }

        Directory.CreateDirectory(destinationDirectory);

        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        Log($"Firmware copiado para: {destinationPath}");
    }

    private void Log(string message)
    {
        LogMessage?.Invoke(this, message);
    }
}
