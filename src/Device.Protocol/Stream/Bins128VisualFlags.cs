namespace Device.Protocol.Stream;

// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
public enum Bins128VisualStyle : byte
{
    LegacyFallback = 0,
    WaveMirror = 1,
    MirrorLines = 2,
    MirrorBlocks = 3,
    ClassicBars = 4,
    FlowLine = 5,
    HistoryScan = 6,
    RadialOrbit = 7,
    Atmosphere = 8,
    LaunchpadGrid = 9,
}

public enum Bins128PaletteFamily : byte
{
    Canonical = 0,
    Rainbow = 1,
    Sunset = 2,
    Arctic = 3,
    Neon = 4,
    Aurora = 5,
    Plasma = 6,
    Mono = 7,
}

public static class Bins128VisualFlags
{
    public static byte Create(Bins128VisualStyle style, Bins128PaletteFamily palette)
    {
        return (byte)((((byte)style) & 0x1F) << 3 | (((byte)palette) & 0x07));
    }

    public static Bins128VisualStyle GetStyle(byte flags)
    {
        var style = (byte)(flags >> 3);
        return Enum.IsDefined(typeof(Bins128VisualStyle), style)
            ? (Bins128VisualStyle)style
            : Bins128VisualStyle.LegacyFallback;
    }

    public static Bins128PaletteFamily GetPalette(byte flags)
    {
        var palette = (byte)(flags & 0x07);
        return Enum.IsDefined(typeof(Bins128PaletteFamily), palette)
            ? (Bins128PaletteFamily)palette
            : Bins128PaletteFamily.Canonical;
    }
}
