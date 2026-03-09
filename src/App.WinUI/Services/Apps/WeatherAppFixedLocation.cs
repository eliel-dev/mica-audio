using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal static class WeatherAppFixedLocation
{
    public const string AppId = "accuweather";
    public const string CityKey = "city";
    public const string UnitsKey = "units";
    public const string FixedLocationLabel = "Timbó, SC";
    public const string FixedCityDisplayName = "Timbó, Santa Catarina, Brasil";
    public const string FixedCityConfigValue = FixedCityDisplayName + "|-26.82333|-49.27167";
    public const string FixedUnitsValue = "metric";
    public const string FixedUnitsLabel = "Celsius (°C)";
    public const double FixedLatitude = -26.82333;
    public const double FixedLongitude = -49.27167;

    public static bool IsWeatherApp(AppCatalogItem? item)
        => item is not null && string.Equals(item.Id, AppId, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> NormalizeRawValues(AppCatalogItem item, IReadOnlyDictionary<string, string>? rawValues)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rawValues is not null)
        {
            foreach (var pair in rawValues)
            {
                normalized[pair.Key] = pair.Value;
            }
        }

        NormalizeRawValuesInPlace(item, normalized);
        return normalized;
    }

    public static void NormalizeRawValuesInPlace(AppCatalogItem item, IDictionary<string, string> rawValues)
    {
        if (!IsWeatherApp(item))
        {
            return;
        }

        rawValues[CityKey] = FixedCityConfigValue;
        rawValues[UnitsKey] = FixedUnitsValue;
    }

    public static void NormalizePayloadInPlace(AppCatalogItem item, IDictionary<string, object?> data)
    {
        if (!IsWeatherApp(item))
        {
            return;
        }

        data[CityKey] = FixedCityConfigValue;
        data[UnitsKey] = FixedUnitsValue;
    }
}
