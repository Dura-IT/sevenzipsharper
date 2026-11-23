using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace SevenZipSharper.Benchmarks
{
    /// <summary>
    /// Runs every benchmark on .NET 10 and on .NET 11 in the same invocation, so both runtimes are measured
    /// back to back under the same conditions.
    /// </summary>
    /// <remarks>
    /// .NET 11 needs BenchmarkDotNet 0.16 (currently a preview): 0.15.8 has no .NET 11 runtime and crashes with
    /// "GetRuntimeVersion not implemented for NotRecognized". Move to the stable 0.16 once it ships.
    /// </remarks>
    public sealed class RuntimeComparisonConfig : ManualConfig
    {
        public RuntimeComparisonConfig()
        {
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0));
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core11_0));
        }
    }
}
