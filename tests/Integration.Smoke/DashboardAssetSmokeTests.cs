using System.Text;

namespace Integration.Smoke;

// DOCS: docs/wiki/reference/device-observability-dashboard.md
public sealed class DashboardAssetSmokeTests
{
    [Fact]
    public void DashboardAssetShouldKeepEspDashSectionAndSingleActionStack()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var htmlPath = Path.Combine(repoRoot, "src", "Device.Server", "wwwroot", "dashboard", "index.html");
        var jsPath = Path.Combine(repoRoot, "src", "Device.Server", "wwwroot", "dashboard", "dashboard.js");

        Assert.True(File.Exists(htmlPath), $"Dashboard asset nao encontrado em {htmlPath}.");
        Assert.True(File.Exists(jsPath), $"Dashboard script nao encontrado em {jsPath}.");

        var html = File.ReadAllText(htmlPath, Encoding.UTF8);
        var js = File.ReadAllText(jsPath, Encoding.UTF8);

        Assert.Contains("id=\"espdash-sec\" style=\"display:none;\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"s-fps\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"s-rssi\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"chart-loop\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"chart-heap\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"btn-led\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"btn-rm\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btn-led-secondary\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btn-rm-secondary\"", html, StringComparison.Ordinal);
        Assert.Contains("HEAP_TOTAL_BYTES_FALLBACK = 320000", js, StringComparison.Ordinal);
        Assert.Contains("PSRAM_TOTAL_BYTES_FALLBACK = 8000000", js, StringComparison.Ordinal);
        Assert.Contains("resolveHeapPercent", js, StringComparison.Ordinal);
        Assert.Contains("resolvePsramPercent", js, StringComparison.Ordinal);
    }
}
