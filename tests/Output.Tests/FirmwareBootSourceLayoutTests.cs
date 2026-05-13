using System.Text;

namespace Output.Tests;

public sealed class FirmwareBootSourceLayoutTests
{
    [Fact]
    public void Setup_ShouldStartHardcodedWifiBeforeInitializingHub75Runtime()
    {
        // STA-hardcoded boot: setup() must connect Wi-Fi from mica_config.h
        // BEFORE bringing up the heavy HUB75 runtime, so the panel comes up
        // already knowing if it has connectivity. The connect call is wrapped
        // inside connectStaHardcoded() which internally calls
        // WiFi.begin(MICA_WIFI_SSID, MICA_WIFI_PASSWORD).
        // DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
        var repoRoot = ResolveRepoRoot();
        var mainPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "main.cpp");
        var source = File.ReadAllText(mainPath, Encoding.UTF8);
        var setupBody = ExtractBetween(source, "void setup()", "void loop()");

        Assert.Contains("loadLightRuntimeStateFromPrefs();", setupBody, StringComparison.Ordinal);
        Assert.Contains("connectStaHardcoded(", setupBody, StringComparison.Ordinal);
        Assert.Contains("initializeHub75RuntimeFromPrefs();", setupBody, StringComparison.Ordinal);
        Assert.Contains("autoRegisterIfNeeded()", setupBody, StringComparison.Ordinal);

        var wifiConnectIndex = setupBody.IndexOf("connectStaHardcoded(", StringComparison.Ordinal);
        var hub75InitIndex = setupBody.IndexOf("initializeHub75RuntimeFromPrefs();", StringComparison.Ordinal);
        Assert.True(
            wifiConnectIndex >= 0 && hub75InitIndex > wifiConnectIndex,
            "STA hardcoded deve subir o Wi-Fi antes de inicializar o runtime pesado do HUB75.");
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
    public void Loop_ShouldPollDisplayStateAfterSignalTimeoutBeforeRender()
    {
        var repoRoot = ResolveRepoRoot();
        var mainPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "main.cpp");
        var source = File.ReadAllText(mainPath, Encoding.UTF8);
        var loopBody = ExtractBetween(source, "void loop()", "  resetTaskWatchdog();");

        Assert.Contains("processSignalTimeout();", loopBody, StringComparison.Ordinal);
        Assert.Contains("pollDisplayStateIfNeeded();", loopBody, StringComparison.Ordinal);
        Assert.Contains("processRenderFrame();", loopBody, StringComparison.Ordinal);

        var timeoutIndex = loopBody.IndexOf("processSignalTimeout();", StringComparison.Ordinal);
        var pollIndex = loopBody.IndexOf("pollDisplayStateIfNeeded();", StringComparison.Ordinal);
        var renderIndex = loopBody.IndexOf("processRenderFrame();", StringComparison.Ordinal);

        Assert.True(
            timeoutIndex >= 0 && pollIndex > timeoutIndex && renderIndex > pollIndex,
            "O firmware deve consultar display-state depois de detectar timeout de frames e antes de renderizar o fallback.");
    }

    [Fact]
    public void FirmwareFallback_ShouldUseServerDisplayStateWhenConnectivityIsHealthy()
    {
        var repoRoot = ResolveRepoRoot();
        var displayPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_display.cpp");
        var source = File.ReadAllText(displayPath, Encoding.UTF8);
        var resolverBody = ExtractBetween(
            source,
            "Hub75FallbackState resolveHub75FallbackCandidate()",
            "Hub75FallbackState resolveHub75FallbackState");

        Assert.Contains("gServerDisplayState != Hub75FallbackState::None", resolverBody, StringComparison.Ordinal);
        Assert.Contains("return gServerDisplayState;", resolverBody, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawConnectivityFallback_ShouldRenderServerIdleScreens()
    {
        var repoRoot = ResolveRepoRoot();
        var displayPath = Path.Combine(repoRoot, "firmware", "esp32s3-devkitc1", "src", "mica_display.cpp");
        var source = File.ReadAllText(displayPath, Encoding.UTF8);
        var drawBody = ExtractBetween(
            source,
            "bool drawConnectivityFallback(Hub75FallbackState state)",
            "// ---------------------------------------------------------------------------\r\n// Frame commit");

        Assert.Contains("case Hub75FallbackState::NoModeActive:", drawBody, StringComparison.Ordinal);
        Assert.Contains("case Hub75FallbackState::FirstRun:", drawBody, StringComparison.Ordinal);
        Assert.Contains("NENHUM MODO", drawBody, StringComparison.Ordinal);
        Assert.Contains("ATIVE PAINEL", drawBody, StringComparison.Ordinal);
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
