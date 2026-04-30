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

    private static readonly byte[] TokenEntropy = Encoding.UTF8.GetBytes("MicaAudio.RemoteDeviceServer.AdminToken.v1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public RemoteDeviceServerSecretStore(IOptions<MicaAudioOptions> options)
    {
        filePath = ResolvePath(options.Value);
    }

    public Task<string> LoadAdminTokenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadAdminToken(filePath));

    public async Task SaveAdminTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var document = new RemoteDeviceServerSecretDocument
        {
            AdminTokenProtected = EncryptToken(token),
        };
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    internal static string LoadAdminToken(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using var fs = File.OpenRead(filePath);
            var document = JsonSerializer.Deserialize<RemoteDeviceServerSecretDocument>(fs, JsonOptions);
            return DecryptToken(document?.AdminTokenProtected);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    internal static string ResolvePath(MicaAudioOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RemoteDeviceServerSecretsFilePath))
        {
            return options.RemoteDeviceServerSecretsFilePath;
        }

        return Path.Combine(options.AppDataRoot, "remote-server-secrets.json");
    }

    private static string EncryptToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var clearBytes = Encoding.UTF8.GetBytes(token.Trim());
        var encryptedBytes = ProtectedData.Protect(clearBytes, TokenEntropy, DataProtectionScope.CurrentUser);
        return TokenCipherPrefix + Convert.ToBase64String(encryptedBytes);
    }

    private static string DecryptToken(string? tokenProtected)
    {
        if (string.IsNullOrWhiteSpace(tokenProtected))
        {
            return string.Empty;
        }

        if (!tokenProtected.StartsWith(TokenCipherPrefix, StringComparison.Ordinal))
        {
            return tokenProtected;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(tokenProtected[TokenCipherPrefix.Length..]);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, TokenEntropy, DataProtectionScope.CurrentUser);
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

    private sealed class RemoteDeviceServerSecretDocument
    {
        public string? AdminTokenProtected { get; init; }
    }
}
