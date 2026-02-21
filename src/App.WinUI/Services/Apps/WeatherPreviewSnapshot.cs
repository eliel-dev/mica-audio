namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed class WeatherPreviewSnapshot
{
    public string CityDisplay { get; init; } = "São Paulo";

    public double? Temperature { get; init; }

    public int? WeatherCode { get; init; }

    public string UnitSymbol { get; init; } = "C";

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.MinValue;

    public bool HasLiveData => Temperature is not null && WeatherCode is not null;
}
