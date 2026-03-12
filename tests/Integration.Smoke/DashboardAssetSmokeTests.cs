using System.Text;

namespace Integration.Smoke;

// DOCS: docs/wiki/reference/device-observability-dashboard.md
public sealed class DashboardAssetSmokeTests
{
    [Fact]
    public void DashboardAssetShouldKeepEspDashSectionAndSingleActionStack()
    {
        var repoRoot = ResolveRepoRoot();
        var htmlPath = Path.Combine(repoRoot, "src", "Device.Server", "wwwroot", "dashboard", "index.html");
        var jsPath = Path.Combine(repoRoot, "src", "Device.Server", "wwwroot", "dashboard", "dashboard.js");

        Assert.True(File.Exists(htmlPath), $"Dashboard asset nao encontrado em {htmlPath}.");
        Assert.True(File.Exists(jsPath), $"Dashboard script nao encontrado em {jsPath}.");

        var html = File.ReadAllText(htmlPath, Encoding.UTF8);
        var js = File.ReadAllText(jsPath, Encoding.UTF8);

        Assert.Contains("id=\"espdash-sec\" style=\"display:none;\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"s-fps\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"s-rssi\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"m-health\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"m-temp\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"firmware-row\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"firmware-current\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"firmware-latest\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"btn-fw\"", html, StringComparison.Ordinal);
        Assert.Contains("Saude do dispositivo", html, StringComparison.Ordinal);
        Assert.Contains("Temperatura do chip", html, StringComparison.Ordinal);
        Assert.Contains("Firmware atual", html, StringComparison.Ordinal);
        Assert.Contains("Firmware oficial", html, StringComparison.Ordinal);
        Assert.Contains("Atualizar firmware", html, StringComparison.Ordinal);
        Assert.Contains("id=\"chart-loop\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"chart-heap\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"btn-led\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"btn-rm\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btn-led-secondary\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btn-rm-secondary\"", html, StringComparison.Ordinal);
        Assert.Contains("HEAP_TOTAL_BYTES_FALLBACK = 320000", js, StringComparison.Ordinal);
        Assert.Contains("PSRAM_TOTAL_BYTES_FALLBACK = 8000000", js, StringComparison.Ordinal);
        Assert.Contains("resolveHealthState", js, StringComparison.Ordinal);
        Assert.Contains("resolveHeapPercent", js, StringComparison.Ordinal);
        Assert.Contains("resolvePsramPercent", js, StringComparison.Ordinal);
        Assert.Contains("loopHealthyPercent", js, StringComparison.Ordinal);
        Assert.Contains("chipTemperatureCelsius", js, StringComparison.Ordinal);
        Assert.Contains("firmwareUpdateAvailable", js, StringComparison.Ordinal);
        Assert.Contains("latestFirmwareVersion", js, StringComparison.Ordinal);
        Assert.Contains("update-firmware", js, StringComparison.Ordinal);
        Assert.DoesNotContain("device.loopLoadPercent", js, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MicaAudio.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Raiz do repositorio nao encontrada a partir do AppContext.BaseDirectory.");
    }
}
