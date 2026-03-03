using App.WinUI.Services.Firmware;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Smoke;

public sealed class FirmwareCatalogSmokeTests
{
    [Fact]
    public void PrecompiledFirmwareService_ShouldReturnOnlyDmaProfile()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var service = provider.GetRequiredService<PrecompiledFirmwareService>();

        var options = service.GetOptions(
            PrecompiledFirmwareService.Esp32S3DevKitC1Board,
            PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32);

        var dma = Assert.Single(options);
        Assert.Equal("dma_exp", dma.Profile);
        Assert.Equal("esp32s3-devkitc1-128x64-dma_exp_merged.bin", dma.FileName);
        Assert.Empty(service.GetOptions(boardModel: "matrixportal_s3"));
    }

    [Fact]
    public void PrecompiledFirmwareService_TryGetOption_ShouldFailForUnknownCombination()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var service = provider.GetRequiredService<PrecompiledFirmwareService>();

        var ok = service.TryGetOption(
            PrecompiledFirmwareService.Esp32S3DevKitC1Board,
            "hub75_64x32",
            "stable",
            out var _,
            out var error);

        Assert.False(ok);
        Assert.Contains("board='esp32s3_devkitc1'", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("painel='hub75_64x32'", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("perfil='stable'", error, StringComparison.OrdinalIgnoreCase);
    }
}
