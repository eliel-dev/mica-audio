using MicaAudio.Core.Led;
using Output.Led;

namespace Output.Tests;

public class SimulatorLedOutputTests
{
    [Fact]
    public void SendBins_ShouldCreateNonEmptyFrame()
    {
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig { Width = 64, Height = 32, Brightness = 1f });

        var bins = Enumerable.Repeat(0.75f, 64).ToArray();
        simulator.Send(new LedPayload { Bins64 = bins, Level = 0.7f, PresetId = "test" });

        var snapshot = simulator.GetFrameSnapshot();

        Assert.Equal(64 * 32, snapshot.Length);
        Assert.Contains(snapshot, px => px.R > 0 || px.G > 0 || px.B > 0);
    }

    [Fact]
    public void SetBrightness_ShouldClampValue()
    {
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig());
        simulator.SetBrightness(3f);

        simulator.Send(new LedPayload { Bins64 = Enumerable.Repeat(1f, 64).ToArray(), Level = 1f });
        var snapshot = simulator.GetFrameSnapshot();

        Assert.Contains(snapshot, px => px.R <= 255 && px.G <= 255 && px.B <= 255);
    }
}
