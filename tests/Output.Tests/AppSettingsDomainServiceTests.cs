using App.WinUI.Services;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class AppSettingsDomainServiceTests
{
    [Fact]
    public void Migrate_ShouldForceFixedSensitivityDefaults_ForAnyInput()
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            Sensitivity = -8f,
            SensitivityMinDb = -42f,
            SensitivityMaxDb = -9f,
            LinearBoost = 2.4f,
            FftSmoothing = 0.55f,
        };

        var result = service.Migrate(source);

        Assert.Equal(-25f, result.Sensitivity);
        Assert.Equal(-85f, result.SensitivityMinDb);
        Assert.Equal(-25f, result.SensitivityMaxDb);
    }

    [Fact]
    public void Migrate_ShouldPreserveNonSensitivitySettings()
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            ActivePresetId = "custom-preset",
            SelectedRendererId = "polar-arcs",
            LinearBoost = 2.2f,
            BarCount = 64,
            FrequencyScale = FrequencyScale.Logarithmic,
            FrequencyMinHz = 40f,
            FrequencyMaxHz = 5000f,
            FftSize = 4096,
            FftSmoothing = 0.4f,
            WeightingFilter = WeightingFilter.C,
        };

        var result = service.Migrate(source);

        Assert.Equal("custom-preset", result.ActivePresetId);
        Assert.Equal("polar-arcs", result.SelectedRendererId);
        Assert.Equal(2.2f, result.LinearBoost);
        Assert.Equal(64, result.BarCount);
        Assert.Equal(FrequencyScale.Logarithmic, result.FrequencyScale);
        Assert.Equal(40f, result.FrequencyMinHz);
        Assert.Equal(5000f, result.FrequencyMaxHz);
        Assert.Equal(4096, result.FftSize);
        Assert.Equal(0.4f, result.FftSmoothing);
        Assert.Equal(WeightingFilter.C, result.WeightingFilter);
        Assert.Equal(-85f, result.SensitivityMinDb);
        Assert.Equal(-25f, result.SensitivityMaxDb);
    }

    [Fact]
    public void Copy_ShouldKeepFixedSensitivityDefaults_WithoutSetSensitivity()
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            Sensitivity = -12f,
            SensitivityMinDb = -70f,
            SensitivityMaxDb = -12f,
            LinearBoost = 1.6f,
        };

        var result = service.Copy(source, builder =>
        {
            builder.SetLinearBoost(2.0f);
            builder.SetBarCount(48);
        });

        Assert.Equal(2.0f, result.LinearBoost);
        Assert.Equal(48, result.BarCount);
        Assert.Equal(-25f, result.Sensitivity);
        Assert.Equal(-85f, result.SensitivityMinDb);
        Assert.Equal(-25f, result.SensitivityMaxDb);
    }
}
