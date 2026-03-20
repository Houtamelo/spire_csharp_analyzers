using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Spire.CodeFixes;
using Spire.SourceGenerators.Analyzers;

namespace Spire.SourceGenerators.Tests;

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
            new ExpandWildcardCodeFix(),
            new FixFieldTypeCodeFix());
}
