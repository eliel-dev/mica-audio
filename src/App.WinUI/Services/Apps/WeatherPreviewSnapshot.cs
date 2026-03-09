namespace App.WinUI.Services.Apps;

internal enum WeatherPreviewLoadState
{
    Loading,
    Live,
    Error,
}

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed class WeatherPreviewSnapshot
{
    public string CityDisplay { get; init; } = WeatherAppFixedLocation.FixedCityDisplayName;

    public double? Temperature { get; init; }

    public int? WeatherCode { get; init; }

    public string UnitSymbol { get; init; } = "C";

    public WeatherPreviewLoadState State { get; init; } = WeatherPreviewLoadState.Loading;

    public string? FailureMessage { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.MinValue;

    public bool HasLiveData => State == WeatherPreviewLoadState.Live && Temperature is not null && WeatherCode is not null;
}
