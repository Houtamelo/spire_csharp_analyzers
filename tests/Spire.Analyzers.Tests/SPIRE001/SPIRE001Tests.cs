using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE001;

public class SPIRE001Tests : AnalyzerTestBase<SPIRE001ArrayOfEnforceInitializationStructAnalyzer>
{
    protected override string RuleId => "SPIRE001";
}
