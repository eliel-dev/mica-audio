using System.Security.Cryptography;
using MicaAudio.Core.Presets;
using Panels.Composition.ServerSide;

namespace Integration.Smoke;

public sealed class WatchfaceLibraryTests
{
    private static readonly DateTime ReferenceTime = new(2025, 5, 23, 20, 48, 12, 120);

    [Fact]
    public void Render_ShouldProduceDenseDistinctFramesForAllReferenceWatchfaces()
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var watchface in WatchfaceLibrary.All)
        {
            var frame = Render(watchface, ReferenceTime);
            var litPixels = frame.Count(static pixel => pixel is { A: > 0 } && (pixel.R | pixel.G | pixel.B) != 0);

            Assert.True(litPixels >= 700, $"{watchface} rendered only {litPixels} lit pixels.");
            Assert.True(hashes.Add(Hash(frame)), $"{watchface} rendered the same frame as another watchface.");
        }
    }

    [Fact]
    public void Render_ShouldFallbackToCyberTerminalForUnknownWatchface()
    {
        var expected = Render("cyberterminal", ReferenceTime);
        var actual = Render("not-a-watchface", ReferenceTime);

        Assert.Equal(Hash(expected), Hash(actual));
    }

    [Theory]
    [MemberData(nameof(AllWatchfaces))]
    public void Render_ShouldAnimateDecorationInsideSameClockSecond(string watchface)
    {
        var first = Render(watchface, ReferenceTime);
        var second = Render(watchface, ReferenceTime.AddMilliseconds(260));

        Assert.NotEqual(Hash(first), Hash(second));
    }

    public static IEnumerable<object[]> AllWatchfaces() =>
        WatchfaceLibrary.All.Select(static watchface => new object[] { watchface });

    private static RgbaColor[] Render(string watchface, DateTime now)
    {
        var frame = new RgbaColor[128 * 64];
        WatchfaceLibrary.Render(watchface, frame, 128, 64, 0, 0, 128, 64, now, use24Hour: true);
        return frame;
    }

    private static string Hash(RgbaColor[] frame)
    {
        var bytes = new byte[frame.Length * 4];
        for (var i = 0; i < frame.Length; i++)
        {
            var offset = i * 4;
            bytes[offset] = frame[i].R;
            bytes[offset + 1] = frame[i].G;
            bytes[offset + 2] = frame[i].B;
            bytes[offset + 3] = frame[i].A;
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
