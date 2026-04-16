using System.Text.Json;

namespace App.WinUI.Infrastructure.Serial;

// DOCS: docs/wiki/guides/setup-new-device.md#logs-seriais-no-wizard
internal static class MicaSerialProtocol
{
    internal const string ProtocolId = "mica.serial.v1";

    public static bool TryParseDocument(string? line, out string type, out JsonDocument? document)
    {
        type = string.Empty;
        document = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(line);
            type = ReadOptionalString(document.RootElement, "type") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(type))
            {
                return true;
            }

            document.Dispose();
            document = null;
            return false;
        }
        catch
        {
            document?.Dispose();
            document = null;
            return false;
        }
    }

    public static bool TryReadHello(JsonElement root, out MicaSerialHello hello)
    {
        hello = new MicaSerialHello();

        var type = ReadOptionalString(root, "type");
        if (!string.Equals(type, "hello", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        hello = new MicaSerialHello
        {
            Protocol = ReadOptionalString(root, "protocol"),
            DeviceId = ReadOptionalString(root, "deviceId"),
            ErrorCode = ReadOptionalString(root, "errorCode"),
        };
        return true;
    }

    public static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.Null
            ? null
            : element.GetString();
    }
}

internal sealed record MicaSerialHello
{
    public string? Protocol { get; init; }

    public string? DeviceId { get; init; }

    public string? ErrorCode { get; init; }
}
