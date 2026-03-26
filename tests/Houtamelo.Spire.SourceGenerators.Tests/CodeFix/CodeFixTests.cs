using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Houtamelo.Spire.CodeFixes;

namespace Houtamelo.Spire.SourceGenerators.Tests.CodeFix;

public class CodeFixTests : CodeFixTestBase
{
    protected override string Category => "CodeFix";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(
            new ExhaustivenessAnalyzer(),
            new CS8509Suppressor(),
            new TypeSafetyAnalyzer());

    protected override ImmutableArray<CodeFixProvider> GetCodeFixes()
        => ImmutableArray.Create<CodeFixProvider>(
            new AddMissingArmsCodeFix(),
            new FixFieldTypeCodeFix());
}
