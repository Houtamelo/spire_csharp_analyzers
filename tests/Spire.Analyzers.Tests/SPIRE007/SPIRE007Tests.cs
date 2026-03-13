using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE007;

public class SPIRE007Tests : AnalyzerTestBase<SPIRE007UnsafeSkipInitOfMustBeInitStructAnalyzer>
{
    protected override string RuleId => "SPIRE007";
}
