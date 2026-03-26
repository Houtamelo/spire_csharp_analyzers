using Houtamelo.Spire.Analyzers.Rules;

namespace Houtamelo.Spire.Analyzers.Tests.SPIRE007;

public class SPIRE007Tests : AnalyzerTestBase<SPIRE007UnsafeSkipInitOfEnforceInitializationStructAnalyzer>
{
    protected override string RuleId => "SPIRE007";
}
