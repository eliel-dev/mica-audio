namespace App.WinUI.Services.Apps;

internal sealed class CitySuggestion
{
    public string Name { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string DisplayName => string.Join(", ", new[] { Name, Region, Country }.Where(static part => !string.IsNullOrWhiteSpace(part)));

    public override string ToString() => DisplayName;

    public string ToConfigValue()
    {
        if (Latitude is null || Longitude is null)
        {
            return DisplayName;
        }

        return $"{DisplayName}|{Latitude.Value:F4}|{Longitude.Value:F4}";
    }
}
