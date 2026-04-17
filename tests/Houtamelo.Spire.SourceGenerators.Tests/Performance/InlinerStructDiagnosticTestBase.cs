using System;
using System.Collections.Immutable;

namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

/// <summary>
/// Abstract base class for [InlinerStruct] generator diagnostic tests. Filters diagnostics
/// to the SPIRE prefix (excluding SPIRE_DU which belongs to the DU generator) and runs the
/// InlinerStructGenerator instead of the DU generator.
/// </summary>
public abstract class InlinerStructDiagnosticTestBase : GeneratorDiagnosticTestBase
{
    protected override string DiagnosticPrefix => "SPIRE";

    protected override void RunGenerator(
        string source,
        out ImmutableArray<RoslynDiagnostic> diagnostics)
    {
        GeneratorTestHelper.RunInlinerStructGenerator(source, out _, out diagnostics, path: "case.cs");
    }

    protected override bool IsRelevantDiagnostic(RoslynDiagnostic d)
        => d.Id.StartsWith("SPIRE", StringComparison.Ordinal)
            && !d.Id.StartsWith("SPIRE_DU", StringComparison.Ordinal);
}
