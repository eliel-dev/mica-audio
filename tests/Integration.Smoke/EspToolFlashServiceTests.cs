using App.WinUI.Services.Devices.Onboarding;

namespace Integration.Smoke;

public sealed class EspToolFlashServiceTests
{
    [Fact]
    public void BuildWriteFlashArguments_ShouldUseExactProfile()
    {
        var args = EspToolFlashService.BuildWriteFlashArguments("COM7", @"C:\fw\merged.bin");

        Assert.Contains("--chip", args);
        Assert.Contains("esp32s3", args);
        Assert.Contains("--port", args);
        Assert.Contains("COM7", args);
        Assert.Contains("--baud", args);
        Assert.Contains("115200", args);
        Assert.Contains("--before", args);
        Assert.Contains("default_reset", args);
        Assert.Contains("--after", args);
        Assert.Contains("hard_reset", args);
        Assert.Contains("write_flash", args);
        Assert.Contains("--no-compress", args);
        Assert.Contains("0x0", args);
        Assert.Contains(@"C:\fw\merged.bin", args);
        Assert.DoesNotContain("-z", args);
    }

    [Fact]
    public void ParseProgressPercent_ShouldAcceptCompactAndSpacedFormats()
    {
        var compact = EspToolFlashService.ParseProgressPercent("Writing at 0x0... (42%)");
        var spaced = EspToolFlashService.ParseProgressPercent("Writing at 0x0... (42 %)");

        Assert.Equal(42, compact);
        Assert.Equal(42, spaced);
    }

    [Fact]
    public void ParseProgressPercent_ShouldIgnoreInvalidAndClamp()
    {
        var missing = EspToolFlashService.ParseProgressPercent("No progress yet");
        var clamped = EspToolFlashService.ParseProgressPercent("Writing at 0x0... (142 %)");

        Assert.Null(missing);
        Assert.Equal(100, clamped);
    }

    [Fact]
    public void BuildFailureMessage_ShouldExplainMissingPythonModule()
    {
        var message = EspToolFlashService.BuildFailureMessage(
            1,
            [@"C:\Python313\python.exe: No module named esptool"]);

        Assert.Equal(
            "Esptool indisponivel no ambiente. Python foi encontrado, mas o modulo 'esptool' nao esta instalado.",
            message);
    }

    [Fact]
    public void BuildFailureMessage_ShouldExplainMissingPythonRuntime()
    {
        var message = EspToolFlashService.BuildFailureMessage(
            9009,
            ["python is not recognized as an internal or external command"]);

        Assert.Equal(
            "Esptool indisponivel no ambiente. Nem esptool.exe nem Python com esptool estao disponiveis.",
            message);
    }

    [Fact]
    public void BuildFailureMessage_ShouldIncludeLastToolLineForGenericFailures()
    {
        var message = EspToolFlashService.BuildFailureMessage(
            2,
            ["Connecting...", "A fatal error occurred: Failed to connect to ESP32-S3"]);

        Assert.Equal(
            "Esptool finalizou com erro (exit code 2): A fatal error occurred: Failed to connect to ESP32-S3",
            message);
    }
}
