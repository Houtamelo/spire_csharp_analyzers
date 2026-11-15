namespace Houtamelo.Spire.SourceGenerators.Tests.RecordStructUnions;

public class RecordStructUnionDiagnosticTests : GeneratorDiagnosticTestBase
{
    protected override string Category => "RecordStructUnions";

    protected override bool IsRelevantDiagnostic(Microsoft.CodeAnalysis.Diagnostic diagnostic)
        => diagnostic.Id == "SPIRE_DU020";
}
