using Houtamelo.Spire.Analyzers.Rules;

namespace Houtamelo.Spire.Analyzers.Tests.SPIRE002;

public class SPIRE002Tests : AnalyzerTestBase<SPIRE002EnforceInitializationOnFieldlessTypeAnalyzer>
{
    protected override string RuleId => "SPIRE002";
}
