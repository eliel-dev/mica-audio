using BenchmarkDotNet.Running;

namespace BenchmarkSuite1
{
    internal sealed class Program
    {
        static void Main(string[] args)
        {
            var _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
