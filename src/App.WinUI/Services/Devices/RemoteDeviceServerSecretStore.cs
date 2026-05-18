using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MicaAudio.Core.Config;
using Microsoft.Extensions.Options;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#tokens-de-dispositivo-em-repouso
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
internal sealed class RemoteDeviceServerSecretStore
{
    private const string TokenCipherPrefix = "dpapi:v1:";

    private static readonly byte[] AdminTokenEntropy =
        Encoding.UTF8.GetBytes("MicaAudio.RemoteDeviceServer.AdminToken.v1");

    private static readonly byte[] GiphyApiKeyEntropy =
        Encoding.UTF8.GetBytes("MicaAudio.GiphyApiKey.v1");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public RemoteDeviceServerSecretStore(IOptions<MicaAudioOptions> options)
    {
        filePath = ResolvePath(options.Value);
    }

    // ── Admin token ────────────────────────────────────────────────────────

    public Task<string> LoadAdminTokenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadAdminToken(filePath));

    public async Task SaveAdminTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var current = LoadDocument(filePath);
        await SaveDocumentAsync(
            current with { AdminTokenProtected = EncryptSecret(token, AdminTokenEntropy) },
            cancellationToken).ConfigureAwait(false);
    }

    // ── GIPHY API key ──────────────────────────────────────────────────────

    public Task<string> LoadGiphyApiKeyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadGiphyApiKey(filePath));

    public async Task SaveGiphyApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var current = LoadDocument(filePath);
        await SaveDocumentAsync(
            current with { GiphyApiKeyProtected = EncryptSecret(apiKey, GiphyApiKeyEntropy) },
            cancellationToken).ConfigureAwait(false);
    }

    // ── Helpers (instance) ─────────────────────────────────────────────────

    private async Task SaveDocumentAsync(RemoteDeviceServerSecretDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    // ── Helpers (static) ───────────────────────────────────────────────────

    internal static string LoadAdminToken(string filePath)
        => DecryptSecret(LoadDocument(filePath).AdminTokenProtected, AdminTokenEntropy);

    internal static string LoadGiphyApiKey(string filePath)
        => DecryptSecret(LoadDocument(filePath).GiphyApiKeyProtected, GiphyApiKeyEntropy);

    internal static string ResolvePath(MicaAudioOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RemoteDeviceServerSecretsFilePath))
        {
            return options.RemoteDeviceServerSecretsFilePath;
        }

        return Path.Combine(options.AppDataRoot, "remote-server-secrets.json");
    }

    private static RemoteDeviceServerSecretDocument LoadDocument(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new RemoteDeviceServerSecretDocument();
        }

        try
        {
            using var fs = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<RemoteDeviceServerSecretDocument>(fs, JsonOptions)
                   ?? new RemoteDeviceServerSecretDocument();
        }
        catch (JsonException)
        {
            return new RemoteDeviceServerSecretDocument();
        }
        catch (IOException)
        {
            return new RemoteDeviceServerSecretDocument();
        }
        catch (UnauthorizedAccessException)
        {
            return new RemoteDeviceServerSecretDocument();
        }
    }

    private static string EncryptSecret(string value, byte[] entropy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var clearBytes = Encoding.UTF8.GetBytes(value.Trim());
        var encryptedBytes = ProtectedData.Protect(clearBytes, entropy, DataProtectionScope.CurrentUser);
        return TokenCipherPrefix + Convert.ToBase64String(encryptedBytes);
    }

    private static string DecryptSecret(string? protectedValue, byte[] entropy)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        if (!protectedValue.StartsWith(TokenCipherPrefix, StringComparison.Ordinal))
        {
            // Legacy plain-text value — return as-is for backward compatibility.
            return protectedValue;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(protectedValue[TokenCipherPrefix.Length..]);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    private sealed record RemoteDeviceServerSecretDocument
    {
        public string? AdminTokenProtected { get; init; }
        public string? GiphyApiKeyProtected { get; init; }
    }
}
