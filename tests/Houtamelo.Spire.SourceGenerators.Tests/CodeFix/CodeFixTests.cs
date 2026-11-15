using System.Collections.Immutable;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Houtamelo.Spire.CodeFixes;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.CodeFix;

public class CodeFixTests : CodeFixTestBase
{
    protected override string Category => "CodeFix";

    [Theory]
    [Trait("Environment", "Unity")]
    [CodeFixCaseDiscovery(requiresUnity: true)]
    public Task VerifyUnity(string caseName) => VerifyCase(caseName);

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(
            new ExhaustivenessAnalyzer(),
            new CS8509Suppressor(),
            new TypeSafetyAnalyzer(),
            new SPIRE015ExhaustiveEnumSwitchAnalyzer(),
            new RecordVariantDeclarationAnalyzer());

    protected override ImmutableArray<CodeFixProvider> GetCodeFixes()
        => ImmutableArray.Create<CodeFixProvider>(
            new AddMissingArmsCodeFix(),
            new FixFieldTypeCodeFix(),
            new RecordVariantSerializationCodeFix(),
            new ReadonlyRecordVariantSerializationCodeFix(),
            new RemoveRecordFromUnionCodeFix());
}
