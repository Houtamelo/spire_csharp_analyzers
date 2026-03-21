using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Spire.Benchmarks.UnionBenchmarks).Assembly).Run(args);
