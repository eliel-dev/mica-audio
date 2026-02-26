using App.WinUI.Services.Firmware;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Smoke;

public sealed class FirmwareCatalogSmokeTests
{
    [Fact]
    public void PrecompiledFirmwareService_ShouldReturnAllExpectedBoardProfileOptions()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var service = provider.GetRequiredService<PrecompiledFirmwareService>();

        var matrixStable = Assert.Single(service.GetOptions(
            PrecompiledFirmwareService.MatrixPortalS3Board,
            PrecompiledFirmwareService.Hub75Panel64x32,
            "stable"));

        var matrixDma = Assert.Single(service.GetOptions(
            PrecompiledFirmwareService.MatrixPortalS3Board,
            PrecompiledFirmwareService.Hub75Panel64x32,
            "dma_exp"));

        var devkitStable = Assert.Single(service.GetOptions(
            PrecompiledFirmwareService.Esp32S3DevKitC1Board,
            PrecompiledFirmwareService.Hub75Panel64x32,
            "stable"));

        var devkitDma = Assert.Single(service.GetOptions(
            PrecompiledFirmwareService.Esp32S3DevKitC1Board,
            PrecompiledFirmwareService.Hub75Panel64x32,
            "dma_exp"));

        Assert.Equal("matrixportal-s3-stable_merged.bin", matrixStable.FileName);
        Assert.Equal("matrixportal-s3-dma_exp_merged.bin", matrixDma.FileName);
        Assert.Equal("esp32s3-devkitc1-stable_merged.bin", devkitStable.FileName);
        Assert.Equal("esp32s3-devkitc1-dma_exp_merged.bin", devkitDma.FileName);
    }

    [Fact]
    public void PrecompiledFirmwareService_TryGetOption_ShouldFailForUnknownCombination()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var service = provider.GetRequiredService<PrecompiledFirmwareService>();

        var ok = service.TryGetOption(
            PrecompiledFirmwareService.Esp32S3DevKitC1Board,
            "hub75_128x64",
            "stable",
            out var _,
            out var error);

        Assert.False(ok);
        Assert.Contains("board='esp32s3_devkitc1'", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("painel='hub75_128x64'", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("perfil='stable'", error, StringComparison.OrdinalIgnoreCase);
    }
}
