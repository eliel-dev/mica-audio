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
    public void Migrate_ShouldRewriteLegacyLinearBoostWhilePreservingOtherSettings()
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
            AllowLegacyWebSocketQueryToken = true,
        };

        var result = service.Migrate(source);

        Assert.Equal("custom-preset", result.ActivePresetId);
        Assert.Equal("polar-arcs", result.SelectedRendererId);
        Assert.Equal(1.3f, result.LinearBoost);
        Assert.Equal(64, result.BarCount);
        Assert.Equal(FrequencyScale.Logarithmic, result.FrequencyScale);
        Assert.Equal(40f, result.FrequencyMinHz);
        Assert.Equal(5000f, result.FrequencyMaxHz);
        Assert.Equal(2048, result.FftSize);
        Assert.Equal(0.4f, result.FftSmoothing);
        Assert.Equal(WeightingFilter.C, result.WeightingFilter);
        Assert.True(result.AllowLegacyWebSocketQueryToken);
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
            AllowLegacyWebSocketQueryToken = true,
        };

        var result = service.Copy(source, builder =>
        {
            builder.SetBarCount(48);
        });

        Assert.Equal(1.3f, result.LinearBoost);
        Assert.Equal(48, result.BarCount);
        Assert.True(result.AllowLegacyWebSocketQueryToken);
        Assert.Equal(-25f, result.Sensitivity);
        Assert.Equal(-85f, result.SensitivityMinDb);
        Assert.Equal(-25f, result.SensitivityMaxDb);
    }

    [Fact]
    public void Copy_ShouldAllowTogglingLegacyWsQueryToken()
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            AllowLegacyWebSocketQueryToken = false,
        };

        var result = service.Copy(source, builder =>
        {
            builder.SetAllowLegacyWebSocketQueryToken(true);
        });

        Assert.True(result.AllowLegacyWebSocketQueryToken);
    }

    [Fact]
    public void Migrate_ShouldPreserveUseMicaBackdropFlag()
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            UseMicaBackdrop = false,
        };

        var result = service.Migrate(source);

        Assert.False(result.UseMicaBackdrop);
    }

    [Fact]
    public void Copy_ShouldAllowTogglingUseMicaBackdrop()
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            UseMicaBackdrop = true,
        };

        var result = service.Copy(source, builder =>
        {
            builder.SetUseMicaBackdrop(false);
        });

        Assert.False(result.UseMicaBackdrop);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.6f)]
    [InlineData(2.4f)]
    public void Migrate_ShouldRewriteAnyLinearBoostToCanonicalValue(float legacyLinearBoost)
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            LinearBoost = legacyLinearBoost,
        };

        var result = service.Migrate(source);

        Assert.Equal(1.3f, result.LinearBoost);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(8192)]
    public void Copy_ShouldRewriteLegacyFftSizeToCanonicalValue(int legacyFftSize)
    {
        var service = new AppSettingsDomainService();
        var source = new AppSettings
        {
            FftSize = legacyFftSize,
        };

        var result = service.Copy(source, builder =>
        {
            builder.SetFftSize(legacyFftSize);
        });

        Assert.Equal(2048, result.FftSize);
    }
}
