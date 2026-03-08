using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;
using Device.Protocol.Models;
namespace Output.Tests;
public sealed class DevicePreviewVisibilityPolicyTests
{
    [Fact]
    public void ShouldShowInlinePreview_ShouldReturnTrue_OnlyWhenOnlineAndPreviewExists()
    {
        var previewItem = new AppCatalogItem { Id = "clock", Name = "Clock" };
        Assert.True(DevicePreviewVisibilityPolicy.ShouldShowInlinePreview(DeviceStatus.Online, previewItem));
        Assert.False(DevicePreviewVisibilityPolicy.ShouldShowInlinePreview(DeviceStatus.Online, null));
        Assert.False(DevicePreviewVisibilityPolicy.ShouldShowInlinePreview(DeviceStatus.Offline, previewItem));
    }
    [Fact]
    public void ShouldShowSelectedPreview_ShouldReturnFalse_WhenOffline()
    {
        var previewItem = new AppCatalogItem { Id = "weather", Name = "Weather" };
        Assert.False(DevicePreviewVisibilityPolicy.ShouldShowSelectedPreview(DeviceStatus.Offline, previewItem));
    }
    [Fact]
    public void BuildSelectedAppLabel_ShouldUseActiveLabel_WhenOnline()
    {
        Assert.Equal("App ativo: Clock", DevicePreviewVisibilityPolicy.BuildSelectedAppLabel(DeviceStatus.Online, "Clock"));
    }
    [Fact]
    public void BuildSelectedAppLabel_ShouldUseLastKnownLabel_WhenOffline()
    {
        Assert.Equal("Ultimo app conhecido: Clock", DevicePreviewVisibilityPolicy.BuildSelectedAppLabel(DeviceStatus.Offline, "Clock"));
    }
    [Fact]
    public void BuildInlinePreviewPlaceholder_ShouldReturnOffline_WhenDeviceIsOffline()
    {
        var previewItem = new AppCatalogItem { Id = "clock", Name = "Clock" };
        Assert.Equal("Offline", DevicePreviewVisibilityPolicy.BuildInlinePreviewPlaceholder(DeviceStatus.Offline, previewItem));
    }
}