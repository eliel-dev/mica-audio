using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly string[] WorkspaceFreshnessInputs =
    [
        Path.Combine("scripts", "build-precompiled-firmware.ps1"),
        Path.Combine("firmware", "esp32s3-devkitc1", "platformio.ini"),
        Path.Combine("firmware", "esp32s3-devkitc1", "src", "main.cpp"),
        Path.Combine("firmware", "esp32s3-devkitc1", "src", "firmware_version.h"),
        Path.Combine("firmware", "esp32s3-devkitc1", "boards"),
        Path.Combine("firmware", "esp32s3-devkitc1", "partitions"),
        Path.Combine("firmware", "esp32s3-devkitc1", "scripts"),
    ];

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
    private readonly object officialFirmwareGate = new();
    private readonly Dictionary<string, OfficialFirmwareStatus> officialFirmwareStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<OfficialFirmwareRefreshResult>> officialFirmwareRefreshTasks = new(StringComparer.OrdinalIgnoreCase);

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

        if (!TryNormalizeManifest(manifest, sourcePath, out var normalizedManifest, out error))
        {
            return false;
        }

        artifact = new ResolvedFirmwareArtifact(option, sourcePath, manifestPath, normalizedManifest);
        return true;
    }

    public bool TryResolveOfficialArtifact(string boardModel, string panelType, string profile, out ResolvedFirmwareArtifact artifact, out string error)
    {
        artifact = null!;
        error = string.Empty;

        if (!TryGetOption(boardModel, panelType, profile, out var option, out error))
        {
            return false;
        }

        return TryResolveOfficialArtifact(option.Id, out artifact, out error);
    }

    public bool TryResolveOfficialArtifact(string optionId, out ResolvedFirmwareArtifact artifact, out string error)
    {
        artifact = null!;
        error = string.Empty;

        if (!TryGetOptionById(optionId, out var option, out error))
        {
            return false;
        }

        var status = GetOrEvaluateOfficialStatus(option);
        if (!status.IsFresh || status.Artifact is null)
        {
            error = status.FailureReason;
            return false;
        }

        artifact = status.Artifact;
        return true;
    }

    public Task<OfficialFirmwareRefreshResult> EnsureOfficialFirmwareFreshAsync(
        string boardModel,
        string panelType,
        string profile,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOption(boardModel, panelType, profile, out var option, out var error))
        {
            return Task.FromResult(new OfficialFirmwareRefreshResult(false, false, error, null));
        }

        return EnsureOfficialFirmwareFreshAsync(option.Id, cancellationToken);
    }

    public async Task<OfficialFirmwareRefreshResult> EnsureOfficialFirmwareFreshAsync(
        string optionId,
        CancellationToken cancellationToken = default)
    {
        Task<OfficialFirmwareRefreshResult> refreshTask;
        lock (officialFirmwareGate)
        {
            if (!officialFirmwareRefreshTasks.TryGetValue(optionId, out refreshTask!))
            {
                refreshTask = EnsureOfficialFirmwareFreshCoreAsync(optionId);
                officialFirmwareRefreshTasks[optionId] = refreshTask;
            }
        }

        try
        {
            return await refreshTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (refreshTask.IsCompleted)
            {
                lock (officialFirmwareGate)
                {
                    if (officialFirmwareRefreshTasks.TryGetValue(optionId, out var currentTask)
                        && ReferenceEquals(currentTask, refreshTask))
                    {
                        officialFirmwareRefreshTasks.Remove(optionId);
                    }
                }
            }
        }
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

    private async Task<OfficialFirmwareRefreshResult> EnsureOfficialFirmwareFreshCoreAsync(string optionId)
    {
        if (!TryGetOptionById(optionId, out var option, out var error))
        {
            var invalidOptionResult = new OfficialFirmwareRefreshResult(false, false, error, null);
            SetOfficialStatus(optionId, new OfficialFirmwareStatus(invalidOptionResult));
            return invalidOptionResult;
        }

        try
        {
            if (!TryResolveWorkspaceContext(out var workspace))
            {
                var packagedStatus = EvaluateOfficialStatus(option, workspace: null);
                SetOfficialStatus(option.Id, packagedStatus);
                return packagedStatus.ToRefreshResult(wasRegenerated: false);
            }

            var currentStatus = EvaluateOfficialStatus(option, workspace);
            if (currentStatus.IsFresh)
            {
                SetOfficialStatus(option.Id, currentStatus);
                return currentStatus.ToRefreshResult(wasRegenerated: false);
            }

            Log($"Release oficial stale ou ausente para '{option.DisplayName}'. Regenerando com o script oficial...");

            var build = await RunOfficialBuildScriptAsync(workspace).ConfigureAwait(false);
            if (!build.Success)
            {
                var failureResult = new OfficialFirmwareRefreshResult(
                    WasRegenerated: false,
                    IsFresh: false,
                    FailureReason: build.FailureReason,
                    ResolvedArtifact: null);
                SetOfficialStatus(option.Id, new OfficialFirmwareStatus(failureResult));
                return failureResult;
            }

            var refreshedStatus = EvaluateOfficialStatus(option, workspace);
            SetOfficialStatus(option.Id, refreshedStatus);
            if (refreshedStatus.IsFresh)
            {
                return refreshedStatus.ToRefreshResult(wasRegenerated: true);
            }

            var staleAfterBuild = refreshedStatus.FailureReason;
            if (!string.IsNullOrWhiteSpace(build.Output))
            {
                staleAfterBuild = $"{staleAfterBuild} Saida do build oficial:{Environment.NewLine}{build.Output}";
            }

            var unresolvedResult = new OfficialFirmwareRefreshResult(
                WasRegenerated: true,
                IsFresh: false,
                FailureReason: staleAfterBuild,
                ResolvedArtifact: null);
            SetOfficialStatus(option.Id, new OfficialFirmwareStatus(unresolvedResult));
            return unresolvedResult;
        }
        catch (Exception ex)
        {
            var failureReason = $"Falha ao garantir frescor do release oficial para '{option.DisplayName}': {ex.Message}";
            LogOfficialFirmwareRefreshFailed(logger, ex, option.Id);
            var exceptionResult = new OfficialFirmwareRefreshResult(
                WasRegenerated: false,
                IsFresh: false,
                FailureReason: failureReason,
                ResolvedArtifact: null);
            SetOfficialStatus(option.Id, new OfficialFirmwareStatus(exceptionResult));
            return exceptionResult;
        }
    }

    private OfficialFirmwareStatus GetOrEvaluateOfficialStatus(PrecompiledFirmwareOption option)
    {
        if (TryResolveWorkspaceContext(out var workspace))
        {
            var evaluatedWorkspaceStatus = EvaluateOfficialStatus(option, workspace);
            SetOfficialStatus(option.Id, evaluatedWorkspaceStatus);
            return evaluatedWorkspaceStatus;
        }

        lock (officialFirmwareGate)
        {
            if (officialFirmwareStatuses.TryGetValue(option.Id, out var cached))
            {
                return cached;
            }
        }

        var evaluated = EvaluateOfficialStatus(option, workspace: null);
        SetOfficialStatus(option.Id, evaluated);
        return evaluated;
    }

    private OfficialFirmwareStatus EvaluateOfficialStatus(PrecompiledFirmwareOption option, WorkspaceContext? workspace)
    {
        if (!TryResolveArtifact(option.Id, out var artifact, out var error))
        {
            return new OfficialFirmwareStatus(
                false,
                workspace is null
                    ? error
                    : $"{error} Release oficial workspace-local indisponivel para '{option.DisplayName}'.",
                null);
        }

        if (workspace is null)
        {
            return new OfficialFirmwareStatus(true, string.Empty, artifact);
        }

        var freshness = EvaluateArtifactFreshness(artifact, workspace);
        return freshness.IsFresh
            ? new OfficialFirmwareStatus(true, string.Empty, artifact)
            : new OfficialFirmwareStatus(false, freshness.FailureReason, null);
    }

    private static ArtifactFreshness EvaluateArtifactFreshness(ResolvedFirmwareArtifact artifact, WorkspaceContext workspace)
    {
        if (!TryResolveArtifactBuildTimestampUtc(artifact, out var artifactBuiltAtUtc))
        {
            return new ArtifactFreshness(
                false,
                $"Manifesto do release oficial invalido para '{artifact.Option.DisplayName}': builtAtUtc ausente ou invalido.");
        }

        if (!TryResolveLatestWorkspaceInputTimestampUtc(workspace, out var latestInputUtc, out var latestInputPath, out var error))
        {
            return new ArtifactFreshness(false, error);
        }

        if (artifactBuiltAtUtc >= latestInputUtc)
        {
            return new ArtifactFreshness(true, string.Empty);
        }

        var relativeInputPath = Path.GetRelativePath(workspace.RepoRoot, latestInputPath);
        return new ArtifactFreshness(
            false,
            $"Release oficial stale para '{artifact.Option.DisplayName}'. O insumo '{relativeInputPath}' mudou em {latestInputUtc:yyyy-MM-dd HH:mm:ss} UTC, depois do manifesto atual ({artifactBuiltAtUtc:yyyy-MM-dd HH:mm:ss} UTC).");
    }

    private bool TryResolveWorkspaceContext([NotNullWhen(true)] out WorkspaceContext? workspace)
    {
        workspace = null;

        foreach (var candidate in EnumerateWorkspaceRootCandidates())
        {
            var repoRoot = Path.GetFullPath(candidate);
            var buildScriptPath = Path.Combine(repoRoot, "scripts", "build-precompiled-firmware.ps1");
            var solutionPath = Path.Combine(repoRoot, "MicaAudio.sln");
            var firmwareRoot = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1");

            if (!File.Exists(solutionPath)
                || !File.Exists(buildScriptPath)
                || !Directory.Exists(firmwareRoot))
            {
                continue;
            }

            workspace = new WorkspaceContext(
                repoRoot,
                buildScriptPath,
                firmwareRoot,
                ResolveOutputRoot(),
                WorkspaceFreshnessInputs.Select(relative => Path.Combine(repoRoot, relative)).ToArray());
            return true;
        }

        return false;
    }

    private IEnumerable<string> EnumerateWorkspaceRootCandidates()
    {
        if (!string.IsNullOrWhiteSpace(options.WorkspaceRoot))
        {
            yield return options.WorkspaceRoot;
        }

        var currentDirectory = Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
        }

        yield return AppContext.BaseDirectory;

        foreach (var root in new[] { options.WorkspaceRoot, currentDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            DirectoryInfo? current;
            try
            {
                current = new DirectoryInfo(Path.GetFullPath(root));
            }
            catch
            {
                continue;
            }

            if (!current.Exists && current.Parent is not null)
            {
                current = current.Parent;
            }

            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }

    private string ResolveOutputRoot()
    {
        if (!string.IsNullOrWhiteSpace(options.PrecompiledFirmwareDirectory))
        {
            return Path.GetFullPath(options.PrecompiledFirmwareDirectory);
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware"));
    }

    private async Task<BuildProcessResult> RunOfficialBuildScriptAsync(WorkspaceContext workspace)
    {
        var output = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolvePowerShellExecutable(),
            WorkingDirectory = workspace.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(workspace.BuildScriptPath);
        startInfo.ArgumentList.Add("-OutputRoot");
        startInfo.ArgumentList.Add(workspace.OutputRoot);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                output.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                output.AppendLine(args.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                return new BuildProcessResult(false, output.ToString(), "Falha ao iniciar o script oficial de build do firmware.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync().ConfigureAwait(false);
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            return new BuildProcessResult(false, output.ToString(), $"Falha ao executar o script oficial de build do firmware: {ex.Message}");
        }

        var capturedOutput = output.ToString().Trim();
        if (process.ExitCode == 0)
        {
            if (!string.IsNullOrWhiteSpace(capturedOutput))
            {
                Log($"Script oficial de firmware concluido com sucesso.{Environment.NewLine}{capturedOutput}");
            }

            return new BuildProcessResult(true, capturedOutput, string.Empty);
        }

        var failure = $"Script oficial de build do firmware falhou com exit code {process.ExitCode}.";
        if (!string.IsNullOrWhiteSpace(capturedOutput))
        {
            failure = $"{failure}{Environment.NewLine}{capturedOutput}";
        }

        LogOfficialFirmwareBuildFailed(logger, process.ExitCode);
        return new BuildProcessResult(false, capturedOutput, failure);
    }

    private static string ResolvePowerShellExecutable()
        => OperatingSystem.IsWindows() ? "powershell" : "pwsh";

    private static bool TryResolveLatestWorkspaceInputTimestampUtc(
        WorkspaceContext workspace,
        out DateTimeOffset latestInputUtc,
        out string latestInputPath,
        out string error)
    {
        latestInputUtc = DateTimeOffset.MinValue;
        latestInputPath = string.Empty;
        error = string.Empty;

        foreach (var inputPath in workspace.FreshnessInputs)
        {
            if (!TryResolvePathLastWriteTimeUtc(inputPath, out var inputLastWriteUtc, out var pathError))
            {
                error = $"Falha ao avaliar frescor do release oficial: {pathError}";
                return false;
            }

            if (inputLastWriteUtc > latestInputUtc)
            {
                latestInputUtc = inputLastWriteUtc;
                latestInputPath = inputPath;
            }
        }

        if (latestInputUtc == DateTimeOffset.MinValue || string.IsNullOrWhiteSpace(latestInputPath))
        {
            error = "Falha ao avaliar frescor do release oficial: nenhum insumo de firmware foi encontrado no workspace.";
            return false;
        }

        return true;
    }

    private static bool TryResolvePathLastWriteTimeUtc(string path, out DateTimeOffset latestUtc, out string error)
    {
        latestUtc = DateTimeOffset.MinValue;
        error = string.Empty;

        try
        {
            if (File.Exists(path))
            {
                latestUtc = File.GetLastWriteTimeUtc(path);
                return true;
            }

            if (!Directory.Exists(path))
            {
                error = $"insumo obrigatorio nao encontrado: {path}";
                return false;
            }

            latestUtc = Directory.GetLastWriteTimeUtc(path);
            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                var entryUtc = File.Exists(entry)
                    ? File.GetLastWriteTimeUtc(entry)
                    : Directory.GetLastWriteTimeUtc(entry);
                if (entryUtc > latestUtc)
                {
                    latestUtc = entryUtc;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"falha ao ler o insumo '{path}': {ex.Message}";
            return false;
        }
    }

    private static bool TryResolveArtifactBuildTimestampUtc(ResolvedFirmwareArtifact artifact, out DateTimeOffset builtAtUtc)
    {
        if (artifact.Manifest.BuiltAtUtc > DateTimeOffset.UnixEpoch)
        {
            builtAtUtc = artifact.Manifest.BuiltAtUtc.ToUniversalTime();
            return true;
        }

        try
        {
            builtAtUtc = new[]
            {
                File.GetLastWriteTimeUtc(artifact.FirmwarePath),
                File.GetLastWriteTimeUtc(artifact.ManifestPath),
            }.Max();
            return true;
        }
        catch (Exception) when (!File.Exists(artifact.FirmwarePath) || !File.Exists(artifact.ManifestPath))
        {
            builtAtUtc = default;
            return false;
        }
    }

    private static bool TryGetOptionById(string optionId, out PrecompiledFirmwareOption option, out string error)
    {
        option = new PrecompiledFirmwareOption();
        error = string.Empty;

        var match = Options.FirstOrDefault(item => string.Equals(item.Id, optionId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            error = $"Opcao de firmware invalida: {optionId}";
            return false;
        }

        option = match;
        return true;
    }

    private void SetOfficialStatus(string optionId, OfficialFirmwareStatus status)
    {
        lock (officialFirmwareGate)
        {
            officialFirmwareStatuses[optionId] = status;
        }
    }

    private static bool TryNormalizeManifest(
        FirmwareArtifactManifest manifest,
        string firmwarePath,
        out FirmwareArtifactManifest normalizedManifest,
        out string error)
    {
        normalizedManifest = manifest;
        error = string.Empty;

        long actualFileSize;
        try
        {
            actualFileSize = new FileInfo(firmwarePath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Falha ao ler metadados do firmware: {ex.Message}";
            return false;
        }

        if (actualFileSize <= 0)
        {
            error = "Arquivo de firmware invalido: tamanho zero.";
            return false;
        }

        if (manifest.FileSizeBytes > 0 && manifest.FileSizeBytes != actualFileSize)
        {
            error = "Manifesto do firmware com tamanho divergente do binario oficial.";
            return false;
        }

        string sha256;
        var normalizedSha256 = manifest.Sha256.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSha256))
        {
            if (!TryComputeSha256(firmwarePath, out sha256, out error))
            {
                return false;
            }
        }
        else
        {
            normalizedSha256 = normalizedSha256.ToLowerInvariant();
            if (!TryComputeSha256(firmwarePath, out sha256, out error))
            {
                return false;
            }

            if (!string.Equals(normalizedSha256, sha256, StringComparison.Ordinal))
            {
                error = "Manifesto do firmware com hash divergente do binario oficial.";
                return false;
            }
        }

        normalizedManifest = new FirmwareArtifactManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            FirmwareVersion = manifest.FirmwareVersion,
            GitSha = manifest.GitSha,
            Profile = manifest.Profile,
            BoardModel = manifest.BoardModel,
            PanelType = manifest.PanelType,
            ControlPlane = manifest.ControlPlane,
            BuiltAtUtc = manifest.BuiltAtUtc,
            Sha256 = sha256,
            FileSizeBytes = actualFileSize,
        };

        return true;
    }

    private static bool TryComputeSha256(string firmwarePath, out string sha256, out string error)
    {
        sha256 = string.Empty;
        error = string.Empty;

        try
        {
            using var stream = File.OpenRead(firmwarePath);
            var hash = SHA256.HashData(stream);
            sha256 = Convert.ToHexString(hash).ToLowerInvariant();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Falha ao calcular hash SHA-256 do firmware: {ex.Message}";
            return false;
        }
    }

    private void Log(string message)
    {
        LogFirmwareMessage(logger, message);
        LogMessage?.Invoke(this, message);
    }

    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "Firmware event: {Message}")]
    private static partial void LogFirmwareMessage(ILogger logger, string message);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Error, Message = "Official firmware refresh failed for {OptionId}")]
    private static partial void LogOfficialFirmwareRefreshFailed(ILogger logger, Exception exception, string optionId);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Warning, Message = "Official firmware build failed. ExitCode={ExitCode}")]
    private static partial void LogOfficialFirmwareBuildFailed(ILogger logger, int exitCode);

    private sealed record OfficialFirmwareStatus(
        bool IsFresh,
        string FailureReason,
        ResolvedFirmwareArtifact? Artifact)
    {
        public OfficialFirmwareStatus(OfficialFirmwareRefreshResult result)
            : this(result.IsFresh, result.FailureReason, result.ResolvedArtifact)
        {
        }

        public OfficialFirmwareRefreshResult ToRefreshResult(bool wasRegenerated)
            => new(wasRegenerated, IsFresh, FailureReason, Artifact);
    }

    private sealed record ArtifactFreshness(bool IsFresh, string FailureReason);

    private sealed record BuildProcessResult(bool Success, string Output, string FailureReason);

    private sealed record WorkspaceContext(
        string RepoRoot,
        string BuildScriptPath,
        string FirmwareRoot,
        string OutputRoot,
        IReadOnlyList<string> FreshnessInputs);
}
