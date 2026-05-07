using System.Text;

namespace Output.Tests;

public sealed class FirmwareBootSourceLayoutTests
{
    [Fact]
    public void Setup_ShouldStartSavedWifiBeforeInitializingHub75Runtime()
    {
        var repoRoot = ResolveRepoRoot();
        var mainPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "main.cpp");
        var source = File.ReadAllText(mainPath, Encoding.UTF8);
        var setupBody = ExtractBetween(source, "void setup()", "void loop()");

        Assert.Contains("loadLightRuntimeStateFromPrefs();", setupBody, StringComparison.Ordinal);
        Assert.Contains("WiFi.begin(", setupBody, StringComparison.Ordinal);
        Assert.Contains("initializeHub75RuntimeFromPrefs();", setupBody, StringComparison.Ordinal);

        var wifiBeginIndex = setupBody.IndexOf("WiFi.begin(", StringComparison.Ordinal);
        var hub75InitIndex = setupBody.IndexOf("initializeHub75RuntimeFromPrefs();", StringComparison.Ordinal);
        Assert.True(
            wifiBeginIndex >= 0 && hub75InitIndex > wifiBeginIndex,
            "O boot provisionado deve tentar subir o Wi-Fi salvo antes de inicializar o runtime pesado do HUB75.");
    }

    [Fact]
    public void InitializeControlCommandRuntime_ShouldNotAllocateSlowCommandQueueAtBoot()
    {
        var repoRoot = ResolveRepoRoot();
        var commandsPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_commands.cpp");
        var source = File.ReadAllText(commandsPath, Encoding.UTF8);
        var functionBody = ExtractBetween(source, "void initializeControlCommandRuntime()", "bool enqueueIncomingControlCommand");

        Assert.DoesNotContain("gSlowCommandQueue = xQueueCreate", functionBody, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeTaskWatchdogRuntime_ShouldReconfigureBeforeInit()
    {
        var repoRoot = ResolveRepoRoot();
        var commandsPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_commands.cpp");
        var source = File.ReadAllText(commandsPath, Encoding.UTF8);
        var functionBody = ExtractBetween(source, "static void initializeTaskWatchdogRuntime()", "static bool ensureControlWorkerStarted()");

        var reconfigureIndex = functionBody.IndexOf("esp_task_wdt_reconfigure(&config)", StringComparison.Ordinal);
        var initIndex = functionBody.IndexOf("esp_task_wdt_init(&config)", StringComparison.Ordinal);

        Assert.True(reconfigureIndex >= 0, "A inicializacao do TWDT deve tentar reconfigurar quando ele ja existir.");
        Assert.True(initIndex >= 0, "A inicializacao do TWDT deve continuar suportando o caso ainda nao inicializado.");
        Assert.True(
            reconfigureIndex < initIndex,
            "A ordem do TWDT deve ser reconfigure -> init para evitar o log ruidoso de already initialized.");
    }

    [Fact]
    public void SessionHeartbeat_ShouldCompleteTrackedCommand()
    {
        var repoRoot = ResolveRepoRoot();
        var commandsPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_commands.cpp");
        var source = File.ReadAllText(commandsPath, Encoding.UTF8);
        var heartbeatBody = ExtractBetween(
            source,
            "if (strcmp(command, \"session_heartbeat\") == 0)",
            "if (strcmp(command, \"session_lock_acquire\") == 0)");

        Assert.Contains("sendCommandProgress(commandId, 100, \"heartbeat\"", heartbeatBody, StringComparison.Ordinal);
    }

    [Fact]
    public void QueuePanelsBatch_ShouldValidateParametersBeforeDetailedLog()
    {
        var repoRoot = ResolveRepoRoot();
        var commandsPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_commands.cpp");
        var source = File.ReadAllText(commandsPath, Encoding.UTF8);
        var batchBody = ExtractBetween(
            source,
            "if (strcmp(command, \"queue_panels_batch\") == 0)",
            "if (!schedulePanelsBatchDownload(commandId, request, command))");

        var validationIndex = batchBody.IndexOf("if (request.panelsSessionId.length() == 0", StringComparison.Ordinal);
        var detailedLogIndex = batchBody.IndexOf("writePanelsBatchCommandLog(request);", StringComparison.Ordinal);

        Assert.True(validationIndex >= 0, "queue_panels_batch deve validar os parametros obrigatorios.");
        Assert.True(detailedLogIndex >= 0, "queue_panels_batch deve manter log detalhado via helper somente apos validacao.");
        Assert.True(
            detailedLogIndex > validationIndex,
            "O log detalhado de queue_panels_batch nao pode acessar Strings do payload antes da validacao.");
    }

    [Fact]
    public void QueuePanelsBatch_ShouldAvoidPrintfForDetailedPayloadLog()
    {
        var repoRoot = ResolveRepoRoot();
        var commandsPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_commands.cpp");
        var source = File.ReadAllText(commandsPath, Encoding.UTF8);
        var batchBody = ExtractBetween(
            source,
            "if (strcmp(command, \"queue_panels_batch\") == 0)",
            "if (!schedulePanelsBatchDownload(commandId, request, command))");

        Assert.DoesNotContain("Serial.printf(\"[cmd] queue_panels_batch", batchBody, StringComparison.Ordinal);
        Assert.Contains("writePanelsBatchCommandLog(request);", batchBody, StringComparison.Ordinal);

        var helperBody = ExtractBetween(
            source,
            "static void writePanelsBatchCommandLog(const PanelsBatchDownloadRequest& request)",
            "static bool isSlowCommandDomainBusy()");
        Assert.DoesNotContain("printf", helperBody, StringComparison.Ordinal);
        Assert.Contains("Serial.print(request.panelsSessionId);", helperBody, StringComparison.Ordinal);
        Assert.Contains("Serial.print(request.downloadUrl);", helperBody, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MicaAudio.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Raiz do repositorio nao encontrada a partir do AppContext.BaseDirectory.");
    }

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Marcador inicial nao encontrado: {startMarker}");

        var endIndex = source.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Marcador final nao encontrado: {endMarker}");

        return source[startIndex..endIndex];
    }
}
