using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE005;

public class SPIRE005Tests : AnalyzerTestBase<SPIRE005ActivatorCreateInstanceOfMustBeInitStructAnalyzer>
{
    protected override string RuleId => "SPIRE005";
}
