using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Firmware;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
internal sealed class PrecompiledFirmwareService
{
    public const string MatrixPortalS3Board = "matrixportal_s3";
    public const string Esp32S3DevKitC1Board = "esp32s3_devkitc1";
    public const string Hub75Panel64x32 = "hub75_64x32";

    private static readonly IReadOnlyList<PrecompiledFirmwareOption> Options =
    [
        new PrecompiledFirmwareOption
        {
            Id = "stable",
            DisplayName = "Matrix Portal S3 - Stable",
            Description = "Perfil est\u00E1vel (Protomatter) para Matrix Portal S3.",
            FileName = "matrixportal-s3-stable_merged.bin",
            BoardModel = MatrixPortalS3Board,
            PanelType = Hub75Panel64x32,
            Profile = "stable",
        },
        new PrecompiledFirmwareOption
        {
            Id = "dma_exp",
            DisplayName = "Matrix Portal S3 - DMA Experimental",
            Description = "Perfil experimental com DMA para Matrix Portal S3.",
            FileName = "matrixportal-s3-dma_exp_merged.bin",
            BoardModel = MatrixPortalS3Board,
            PanelType = Hub75Panel64x32,
            Profile = "dma_exp",
        },
        new PrecompiledFirmwareOption
        {
            Id = "esp32s3_devkitc1_stable",
            DisplayName = "ESP32-S3 DevKitC-1 - Stable",
            Description = "Perfil est\u00E1vel para ESP32-S3 DevKitC-1 v1.0 (WROOM-1).",
            FileName = "esp32s3-devkitc1-stable_merged.bin",
            BoardModel = Esp32S3DevKitC1Board,
            PanelType = Hub75Panel64x32,
            Profile = "stable",
        },
        new PrecompiledFirmwareOption
        {
            Id = "esp32s3_devkitc1_dma_exp",
            DisplayName = "ESP32-S3 DevKitC-1 - DMA Experimental",
            Description = "Perfil DMA experimental para ESP32-S3 DevKitC-1 v1.0 (WROOM-1).",
            FileName = "esp32s3-devkitc1-dma_exp_merged.bin",
            BoardModel = Esp32S3DevKitC1Board,
            PanelType = Hub75Panel64x32,
            Profile = "dma_exp",
        },
    ];

    private readonly MicaAudioOptions options;
    private readonly ILogger<PrecompiledFirmwareService> logger;

    public PrecompiledFirmwareService(IOptions<MicaAudioOptions> options, ILogger<PrecompiledFirmwareService> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public event EventHandler<string>? LogMessage;

    public IReadOnlyList<PrecompiledFirmwareOption> GetOptions(
        string? boardModel = null,
        string? panelType = null,
        string? profile = null)
    {
        IEnumerable<PrecompiledFirmwareOption> query = Options;

        if (!string.IsNullOrWhiteSpace(boardModel))
        {
            query = query.Where(item => string.Equals(item.BoardModel, boardModel, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(panelType))
        {
            query = query.Where(item => string.Equals(item.PanelType, panelType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(profile))
        {
            query = query.Where(item => string.Equals(item.Profile, profile, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToArray();
    }

    public bool TryGetOption(string boardModel, string panelType, string profile, out PrecompiledFirmwareOption option, out string error)
    {
        option = new PrecompiledFirmwareOption();
        error = string.Empty;

        var match = Options.FirstOrDefault(item =>
            string.Equals(item.BoardModel, boardModel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.PanelType, panelType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Profile, profile, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            error = $"Nao existe firmware precompilado para board='{boardModel}', painel='{panelType}', perfil='{profile}'.";
            return false;
        }

        option = match;
        return true;
    }

    public bool TryResolveSource(string boardModel, string panelType, string profile, out PrecompiledFirmwareOption option, out string sourcePath, out string error)
    {
        sourcePath = string.Empty;
        if (!TryGetOption(boardModel, panelType, profile, out option, out error))
        {
            return false;
        }

        if (TryResolveSource(option.Id, out sourcePath, out error))
        {
            return true;
        }

        error = $"{error} (board='{boardModel}', painel='{panelType}', perfil='{profile}').";
        return false;
    }

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

        var configuredFirmwareDirectory = options.PrecompiledFirmwareDirectory;
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredFirmwareDirectory))
        {
            candidates.Add(Path.Combine(configuredFirmwareDirectory, option.FileName));
        }

        candidates.AddRange(
        [
            Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware", option.FileName),
            Path.Combine(Environment.CurrentDirectory, "AppData", "Firmware", option.FileName),
            Path.Combine(Environment.CurrentDirectory, "src", "App.WinUI", "AppData", "Firmware", option.FileName),
        ]);

        foreach (var candidate in candidates
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
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
        logger.LogInformation("{Message}", message);
        LogMessage?.Invoke(this, message);
    }
}



