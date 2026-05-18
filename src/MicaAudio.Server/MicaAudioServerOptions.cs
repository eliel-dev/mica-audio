namespace MicaAudio.Server;

// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#fase-1---server-standalone-cloud-ready-sem-mudar-wire
// DOCS: docs/handoffs/2026-04-22-micaudio-server-standalone.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-22-micaudio-server-docker-advertised-endpoints.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public sealed class MicaAudioServerOptions
{
    public const string EnvironmentVariablePrefix = "MICA_SERVER__";

    public string ListenHost { get; set; } = "0.0.0.0";

    public int Port { get; set; } = 5272;

    public int MqttPort { get; set; } = 5273;

    public int MaxDevices { get; set; } = 5;

    public string MdnsServiceName { get; set; } = "_micaaudio._tcp";

    public string PublicHost { get; set; } = string.Empty;

    public string PublicHttpBaseAddress { get; set; } = string.Empty;

    public string MqttRootTopic { get; set; } = "mica/v1/devices";

    public string AdminToken { get; set; } = string.Empty;

    public bool RestrictToPrivateNetworks { get; set; } = true;

    public int VisualUdpPort { get; set; } = 5274;

    public bool PreferLanUdpVisualTransport { get; set; }

    public string[] AllowedCidrs { get; set; } = Array.Empty<string>();

    public int PairRequestsPerMinute { get; set; } = 20;

    public int CommandAckRequestsPerSecond { get; set; } = 40;

    public int WebSocketHandshakesPerMinute { get; set; } = 40;

    public int PairingAttemptsPerWindow { get; set; } = 12;

    public int PairingAttemptWindowSeconds { get; set; } = 60;

    public int DeviceFreshThresholdSeconds { get; set; } = 15;

    public bool AllowLegacyWebSocketQueryToken { get; set; }

    public long MaxJsonBodyBytes { get; set; } = 64L * 1024L;

    public int MaxWebSocketMessageBytes { get; set; } = 64 * 1024;

    public int StartupPairCodeTtlSeconds { get; set; } = 600;

    public string StorageRoot { get; set; } = GetDefaultStorageRoot();

    /// <summary>
    /// GIPHY API key used by the server to proxy GIF search requests.
    /// Configure via environment variable <c>MICA_SERVER__GiphyApiKey</c>.
    /// When empty, the /api/v1/admin/giphy/search endpoint returns 503.
    /// </summary>
    public string GiphyApiKey { get; set; } = string.Empty;

    private static string GetDefaultStorageRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localAppData, "MicaAudio", "Server");
    }
}
