using BenchmarkDotNet.Running;

// Run all:           dotnet run -c Release
// Single class:      dotnet run -c Release -- --filter *Construct*
// Micro with disasm: dotnet run -c Release -- --filter *MicroConstruct*
// JSON only:         dotnet run -c Release -- --filter *Json*
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
