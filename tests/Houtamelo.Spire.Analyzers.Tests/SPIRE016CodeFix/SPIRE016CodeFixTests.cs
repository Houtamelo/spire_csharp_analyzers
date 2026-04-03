using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Tests;

public class SPIRE016CodeFixTests : AnalyzerCodeFixTestBase
{
    protected override string Category => "SPIRE016CodeFix";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers() =>
        ImmutableArray.Create<DiagnosticAnalyzer>(
            new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer());

    protected override ImmutableArray<CodeFixProvider> GetCodeFixes() =>
        ImmutableArray.Create<CodeFixProvider>(new FromEnumCodeFix());
}
