using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace Houtamelo.Spire.Benchmarks;

public class SpireConfig : ManualConfig
{
    public SpireConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(CategoriesColumn.Default);
        AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByCategory);
    }
}

public class SpireDisasmConfig : SpireConfig
{
    public SpireDisasmConfig()
    {
        AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
            maxDepth: 3, printSource: true)));
    }
}
