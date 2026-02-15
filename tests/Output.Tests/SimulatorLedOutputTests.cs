using MicaAudio.Core.Led;
using Output.Led;

namespace Output.Tests;

public class SimulatorLedOutputTests
{
    [Fact]
    public void SendBins_ShouldCreateNonEmptyFrame()
    {
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig
        {
            Width = LedDefaults.MatrixWidth,
            Height = LedDefaults.MatrixHeight,
            Brightness = LedDefaults.Brightness,
        });

        var bins = Enumerable.Repeat(0.75f, LedDefaults.MatrixWidth).ToArray();
        simulator.Send(new LedPayload { Bins64 = bins, Level = 0.7f, PresetId = "test" });

        var snapshot = simulator.GetFrameSnapshot();

        Assert.Equal(LedDefaults.MatrixWidth * LedDefaults.MatrixHeight, snapshot.Length);
        Assert.Contains(snapshot, px => px.R > 0 || px.G > 0 || px.B > 0);
    }

    [Fact]
    public void SetBrightness_ShouldClampValue()
    {
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig());
        simulator.SetBrightness(3f);

        simulator.Send(new LedPayload { Bins64 = Enumerable.Repeat(1f, LedDefaults.MatrixWidth).ToArray(), Level = 1f });
        var snapshot = simulator.GetFrameSnapshot();

        Assert.Contains(snapshot, px => px.R <= 255 && px.G <= 255 && px.B <= 255);
    }

    [Fact]
    public void SimulatorLedOutput_ShouldNotExposeFrameUpdatedEvent()
    {
        var frameUpdatedEvent = typeof(SimulatorLedOutput).GetEvent("FrameUpdated");

        Assert.Null(frameUpdatedEvent);
    }
}
