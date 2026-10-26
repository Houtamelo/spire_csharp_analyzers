using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Houtamelo.Spire;
using Houtamelo.Spire.PatternAnalysis;
using Houtamelo.Spire.TestFixtures.NestedRecursive;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Xunit;
#if SPIRE_UNITY_TEST_HOST
using Spire.Tests.Hosting;
#endif

namespace Houtamelo.Spire.PatternAnalysis.Tests.Integration.NestedRecursive;

// This assembly is an existing InternalsVisibleTo target, so checker assertions
// use ExhaustivenessChecker.Check directly rather than reflection.
public sealed class PatternAnalysisNestedRecursiveCheckerTests
{
    [Fact]
    public void NestedBoolLeaf_AllValuesCovered_IsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.NestedBoolComplete, missingCasesEmpty: true, determinate: true);

    [Fact]
    public void NestedEnumLeaf_MissingMember_IsIncompleteAndDeterminate()
        => AssertResult(NestedRecursiveSources.NestedEnumMissing, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void NullableParent_NullAndNestedBoolCovered_IsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.NullableParentComplete, missingCasesEmpty: true, determinate: true);

    [Fact]
    public void NullableRoot_NullAndNestedLeafCovered_IsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.NullableRootComplete, missingCasesEmpty: true, determinate: true);

    [Fact]
    public void SameLeafSymbolOnIndependentPaths_DoesNotAlias()
        => AssertResult(NestedRecursiveSources.SiblingMissing, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void CorrelatedEnumAndBoolLeaves_MissingValidPair_IsIncomplete()
        => AssertResult(NestedRecursiveSources.CorrelatedMissing, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void DerivedTypeParent_NestedLeavesRemainCorrelated()
        => AssertResult(NestedRecursiveSources.DerivedMissing, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void DerivedTypeGuard_DoesNotProveNestedCoverage()
        => AssertResult(NestedRecursiveSources.DerivedGuardMissing, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void UnsupportedUserDefinedDeconstruction_IsIndeterminate()
    {
        var (compilation, tree) = Compile(NestedRecursiveSources.UnsupportedDeconstruction);
        var operation = GetSwitch(compilation, tree);
        var result = ExhaustivenessChecker.Check(compilation, operation);

        Assert.False(result.IsDeterminate);
        // Unknown analysis must not fabricate a concrete MissingCase. The
        // result may be empty while IsDeterminate is false.
    }

    [Fact]
    public void UnsupportedUserDefinedDeconstruction_WithDiscardIsExhaustiveAndDeterminate()
        => AssertResult(
            NestedRecursiveSources.UnsupportedDeconstructionWithDiscardComplete,
            missingCasesEmpty: true,
            determinate: true);

    [Fact]
    public void ManualDiscriminatedUnionDeconstructionMissingPayload_IsNotProvenExhaustive()
        => AssertNotProven(NestedRecursiveSources.ManualStructUnionDeconstructionMissing);

    [Fact]
    public void ManualNullableDiscriminatedUnionDeconstructionMissingPayload_IsNotProvenExhaustive()
        => AssertNotProven(NestedRecursiveSources.ManualNullableStructUnionDeconstructionMissing);

    [Fact]
    public void NegatedEnumComplement_IsNotProvenExhaustive()
        => AssertNotProven(NestedRecursiveSources.NegatedEnumComplementMissing);

    [Fact]
    public void ExplicitDiscardWildcard_RemainsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.ExplicitDiscard, missingCasesEmpty: true, determinate: true);

    [Fact]
    public void GuardedArm_DoesNotProveNestedBoolCoverage()
        => AssertResult(NestedRecursiveSources.GuardedNested, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void NumericBoolCorrelation_DoubleLeavesFiniteHoles()
        => AssertResult(
            NestedRecursiveSources.NumericBoolCorrelationDoubleMissing,
            missingCasesEmpty: false,
            determinate: true);

    [Fact]
    public void NumericBoolCorrelation_FloatLeavesFiniteHoles()
        => AssertResult(
            NestedRecursiveSources.NumericBoolCorrelationFloatMissing,
            missingCasesEmpty: false,
            determinate: true);

    [Fact]
    public void ExtendedPropertySiblingSharedLeaf_IsNotProvenExhaustive()
        => AssertNotProven(NestedRecursiveSources.ExtendedPropertySiblingSharedLeafMissing);

    [Fact]
    public void ExtendedPropertyNullableSibling_IsNotProvenExhaustive()
        => AssertNotProven(NestedRecursiveSources.ExtendedPropertyNullableSiblingMissing);

    [Fact]
    public void NestedStringLiteralRestriction_IsNotProvenExhaustive()
        => AssertNotProven(NestedRecursiveSources.NestedStringLiteralMissing);

    [Fact]
    public void EnforcedHierarchy_PositionalDeconstructionRestriction_IsNotProvenExhaustive()
        => AssertNotProven(
            NestedRecursiveSources.EnforcedHierarchyPositionalDeconstructMissing);

    [Fact]
    public void EnforcedHierarchy_PositionalAndPropertyRestriction_IsNotProvenExhaustive()
        => AssertNotProven(
            NestedRecursiveSources.EnforcedHierarchyPositionalAndPropertyComplete);

    [Fact]
    public void RuntimeBoolNullableAndSiblingFiniteMatrix_IsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.RuntimeBoolNullableSiblingComplete, missingCasesEmpty: true, determinate: true);

    [Fact]
    public void RuntimeEnumNestedFiniteMatrix_IsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.RuntimeEnumDerivedComplete, missingCasesEmpty: true, determinate: true);

    [Fact]
    public void MissingNull_RemainsIncomplete()
        => AssertResult(NestedRecursiveSources.MissingNullableParent, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void MissingNestedBool_RemainsIncomplete()
        => AssertResult(NestedRecursiveSources.MissingBool, missingCasesEmpty: false, determinate: true);

    [Fact]
    public void RestoredNullAndNestedBool_IsExhaustiveAndDeterminate()
        => AssertResult(NestedRecursiveSources.RestoredNullableParentComplete, missingCasesEmpty: true, determinate: true);

    private static void AssertResult(
        string source,
        bool missingCasesEmpty,
        bool determinate)
    {
        var (compilation, tree) = Compile(source);
        var switches = tree.GetRoot().DescendantNodes()
            .OfType<SwitchExpressionSyntax>()
            .ToArray();
        Assert.NotEmpty(switches);

        foreach (var syntax in switches)
        {
            var operation = GetSwitch(compilation, tree, syntax);
            var result = ExhaustivenessChecker.Check(compilation, operation);
            Assert.Equal(missingCasesEmpty, result.MissingCases.IsEmpty);
            Assert.Equal(determinate, result.IsDeterminate);
        }
    }

    private static void AssertNotProven(string source)
    {
        var (compilation, tree) = Compile(source);
        var switches = tree.GetRoot().DescendantNodes()
            .OfType<SwitchExpressionSyntax>()
            .ToArray();
        Assert.NotEmpty(switches);

        foreach (var syntax in switches)
        {
            var result = ExhaustivenessChecker.Check(
                compilation,
                GetSwitch(compilation, tree, syntax));
            Assert.False(result.IsDeterminate && result.MissingCases.IsEmpty);
        }
    }

    private static ISwitchExpressionOperation GetSwitch(
        CSharpCompilation compilation,
        SyntaxTree tree)
    {
        var syntax = tree.GetRoot().DescendantNodes()
            .OfType<SwitchExpressionSyntax>()
            .Single();
        return GetSwitch(compilation, tree, syntax);
    }

    private static ISwitchExpressionOperation GetSwitch(
        CSharpCompilation compilation,
        SyntaxTree tree,
        SwitchExpressionSyntax syntax)
    {
        var operation = compilation.GetSemanticModel(tree).GetOperation(syntax);
        Assert.NotNull(operation);
        return (ISwitchExpressionOperation)operation!;
    }

    private static (CSharpCompilation Compilation, SyntaxTree Tree) Compile(string source)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "nested-case.cs");
        var compilation = CSharpCompilation.Create(
            "PatternAnalysisNestedRecursiveCheckerTests",
            new[] { tree },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(errors.Length == 0,
            "Test source must compile without errors:\n" +
            string.Join("\n", errors.Select(d => $"{d.Id}: {d.GetMessage()}")));
        return (compilation, tree);
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
#if SPIRE_UNITY_TEST_HOST
        var references = UnityTestHost.FrameworkReferences;
#else
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        Assert.False(string.IsNullOrWhiteSpace(tpa));
        var references = tpa!
            .Split(Path.PathSeparator)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
#endif
        return references.Add(MetadataReference.CreateFromFile(
            typeof(EnforceExhaustivenessAttribute).Assembly.Location));
    }
}
