using Device.Protocol.Stream;

namespace Output.Tests;

public sealed class Bins128VisualFlagsTests
{
    [Fact]
    public void Create_ShouldPackStyleAndPaletteIntoSingleByte()
    {
        var flags = Bins128VisualFlags.Create(
            Bins128VisualStyle.Atmosphere,
            Bins128PaletteFamily.Plasma);

        Assert.Equal(Bins128VisualStyle.Atmosphere, Bins128VisualFlags.GetStyle(flags));
        Assert.Equal(Bins128PaletteFamily.Plasma, Bins128VisualFlags.GetPalette(flags));
    }

    [Fact]
    public void UnknownStyleOrPalette_ShouldFallbackToSafeDefaults()
    {
        const byte flags = 0xFF;

        Assert.Equal(Bins128VisualStyle.LegacyFallback, Bins128VisualFlags.GetStyle(flags));
        Assert.Equal(Bins128PaletteFamily.Mono, Bins128VisualFlags.GetPalette(flags));
    }
}
