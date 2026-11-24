using BenchmarkDotNet.Running;

await BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAsync(args);
