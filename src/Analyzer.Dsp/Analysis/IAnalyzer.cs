using MicaAudio.Core.Audio;

namespace Analyzer.Dsp.Analysis;

public interface IAnalyzer
{
    SpectrumFrame? Process(in PcmFrame frame);
}
