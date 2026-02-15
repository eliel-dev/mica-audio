namespace Device.Protocol.Models;

public sealed class DeviceAppCommandPayload
{
    public string AppId { get; init; } = string.Empty;

    public string PackageName { get; init; } = string.Empty;

    public int? RecommendedIntervalMinutes { get; init; }

    public string? ConfigJson { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyDictionary<string, string> ToParameters()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["appId"] = AppId,
            ["packageName"] = PackageName,
        };

        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            parameters["displayName"] = DisplayName;
        }

        if (RecommendedIntervalMinutes is not null)
        {
            parameters["recommendedInterval"] = RecommendedIntervalMinutes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(ConfigJson))
        {
            parameters["configJson"] = ConfigJson;
        }

        return parameters;
    }
}

