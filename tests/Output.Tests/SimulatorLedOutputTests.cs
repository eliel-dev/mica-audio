using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

public class SimulatorLedOutputTests
{
    [Fact]
    public void SendBins_ShouldCreateNonEmptyFrame()
    {
        var simulator = CreateStartedSimulator();

        var bins = Enumerable.Repeat(0.75f, LedDefaults.MatrixWidth).ToArray();
        simulator.Send(new LedPayload { Bins64 = bins, Level = 0.7f, PresetId = "test" });

        var snapshot = simulator.GetFrameSnapshot();

        Assert.Equal(LedDefaults.MatrixWidth * LedDefaults.MatrixHeight, snapshot.Length);
        Assert.Contains(snapshot, px => px.R > 0 || px.G > 0 || px.B > 0);
    }

    [Fact]
    public void SetBrightness_Zero_ShouldTurnOffBothBinsAndFramePayload()
    {
        var simulator = CreateStartedSimulator();
        simulator.SetBrightness(0f);

        simulator.Send(new LedPayload
        {
            Bins64 = Enumerable.Repeat(1f, LedDefaults.MatrixWidth).ToArray(),
            Level = 1f,
        });

        var binsSnapshot = simulator.GetFrameSnapshot();
        Assert.All(binsSnapshot, px => Assert.True(px.R == 0 && px.G == 0 && px.B == 0));

        var sourceFrame = Enumerable
            .Repeat(new RgbaColor(200, 120, 60, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
            .ToArray();

        simulator.Send(new LedPayload { Frame64x32 = sourceFrame });

        var frameSnapshot = simulator.GetFrameSnapshot();
        Assert.All(frameSnapshot, px => Assert.True(px.R == 0 && px.G == 0 && px.B == 0));
    }

    [Fact]
    public void SetBrightness_AboveOne_ShouldBehaveAsBrightnessOne_ForBinsAndFramePayload()
    {
        var bins = Enumerable.Repeat(1f, LedDefaults.MatrixWidth).ToArray();
        var sourceFrame = Enumerable
            .Repeat(new RgbaColor(220, 110, 40, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
            .ToArray();

        var binsAtOne = CreateStartedSimulator();
        binsAtOne.SetBrightness(1f);
        binsAtOne.Send(new LedPayload { Bins64 = bins, Level = 1f });
        var binsExpected = binsAtOne.GetFrameSnapshot();

        var binsOver = CreateStartedSimulator();
        binsOver.SetBrightness(3f);
        binsOver.Send(new LedPayload { Bins64 = bins, Level = 1f });
        var binsActual = binsOver.GetFrameSnapshot();

        Assert.Equal(binsExpected, binsActual);

        var frameAtOne = CreateStartedSimulator();
        frameAtOne.SetBrightness(1f);
        frameAtOne.Send(new LedPayload { Frame64x32 = sourceFrame });
        var frameExpected = frameAtOne.GetFrameSnapshot();

        var frameOver = CreateStartedSimulator();
        frameOver.SetBrightness(3f);
        frameOver.Send(new LedPayload { Frame64x32 = sourceFrame });
        var frameActual = frameOver.GetFrameSnapshot();

        Assert.Equal(frameExpected, frameActual);
    }

    [Fact]
    public void SimulatorLedOutput_ShouldNotExposeFrameUpdatedEvent()
    {
        var frameUpdatedEvent = typeof(SimulatorLedOutput).GetEvent("FrameUpdated");

        Assert.Null(frameUpdatedEvent);
    }

    private static SimulatorLedOutput CreateStartedSimulator()
    {
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig
        {
            Width = LedDefaults.MatrixWidth,
            Height = LedDefaults.MatrixHeight,
            Brightness = LedDefaults.Brightness,
        });

        return simulator;
    }
}
