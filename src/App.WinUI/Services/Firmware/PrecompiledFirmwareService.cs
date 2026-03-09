using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Firmware;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
// DOCS: docs/wiki/guides/setup-new-device.md#referencias-de-codigo
internal sealed partial class PrecompiledFirmwareService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public const string Esp32S3DevKitC1Board = "esp32s3_devkitc1";
    public const string Hub75PanelP25_128x64_Smd2121_Scan32 = "hub75_p2_5_128x64_smd2121_scan32";
    public const string RequiredControlPlane = "mqtt";

    private static readonly IReadOnlyList<PrecompiledFirmwareOption> Options =
    [
        new PrecompiledFirmwareOption
        {
            Id = "esp32s3_devkitc1_128x64_dma_exp",
            DisplayName = "ESP32-S3 DevKitC-1 128x64 - DMA",
            Description = "Perfil oficial unico em DMA para ESP32-S3 DevKitC-1/WROOM-1 no painel P2.5 128x64 1/32.",
            FileName = "esp32s3-devkitc1-128x64-dma_exp_merged.bin",
            BoardModel = Esp32S3DevKitC1Board,
            PanelType = Hub75PanelP25_128x64_Smd2121_Scan32,
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

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "O service permanece por instancia porque outras operacoes dependem de opcoes e logging injetados, e o contrato publico deve ficar consistente.")]
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

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "O service permanece por instancia porque outras operacoes dependem de opcoes e logging injetados, e o contrato publico deve ficar consistente.")]
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

    public bool TryResolveArtifact(string boardModel, string panelType, string profile, out ResolvedFirmwareArtifact artifact, out string error)
    {
        artifact = null!;
        error = string.Empty;

        if (!TryGetOption(boardModel, panelType, profile, out var option, out error))
        {
            return false;
        }

        return TryResolveArtifact(option.Id, out artifact, out error);
    }

    public bool TryResolveArtifact(string optionId, out ResolvedFirmwareArtifact artifact, out string error)
    {
        artifact = null!;
        error = string.Empty;

        if (!TryResolveSource(optionId, out var sourcePath, out error))
        {
            return false;
        }

        var option = Options.First(item => string.Equals(item.Id, optionId, StringComparison.OrdinalIgnoreCase));
        var manifestPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, GetManifestFileName(option.FileName));
        if (!File.Exists(manifestPath))
        {
            error = $"Manifesto do firmware nao encontrado para '{option.DisplayName}' ({Path.GetFileName(manifestPath)}).";
            return false;
        }

        FirmwareArtifactManifest? manifest;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            manifest = JsonSerializer.Deserialize<FirmwareArtifactManifest>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            error = $"Manifesto do firmware invalido para '{option.DisplayName}' ({Path.GetFileName(manifestPath)}).";
            return false;
        }
        catch (IOException ex)
        {
            error = $"Falha ao abrir manifesto do firmware: {ex.Message}";
            return false;
        }

        if (manifest is null)
        {
            error = $"Manifesto do firmware vazio para '{option.DisplayName}'.";
            return false;
        }

        if (!string.Equals(manifest.Profile, option.Profile, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.BoardModel, option.BoardModel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.PanelType, option.PanelType, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Manifesto do firmware nao corresponde ao pacote esperado para '{option.DisplayName}'.";
            return false;
        }

        artifact = new ResolvedFirmwareArtifact(option, sourcePath, manifestPath, manifest);
        return true;
    }

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

    public static string GetManifestFileName(string firmwareFileName)
    {
        if (string.IsNullOrWhiteSpace(firmwareFileName))
        {
            throw new ArgumentException("Nome do firmware invalido.", nameof(firmwareFileName));
        }

        return Path.ChangeExtension(firmwareFileName, "manifest.json");
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
        LogFirmwareMessage(logger, message);
        LogMessage?.Invoke(this, message);
    }

    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "Firmware event: {Message}")]
    private static partial void LogFirmwareMessage(ILogger logger, string message);
}
