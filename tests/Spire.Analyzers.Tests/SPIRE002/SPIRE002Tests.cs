using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE002;

public class SPIRE002Tests : AnalyzerTestBase<SPIRE002MustBeInitOnFieldlessTypeAnalyzer>
{
    protected override string RuleId => "SPIRE002";
}
