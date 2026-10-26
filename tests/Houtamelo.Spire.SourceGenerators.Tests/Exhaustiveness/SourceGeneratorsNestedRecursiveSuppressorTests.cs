using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.TestFixtures.NestedRecursive;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

// Checker-result assertions live in PatternAnalysis.Tests, where the existing
// InternalsVisibleTo grant permits direct ExhaustivenessChecker.Check calls.
public sealed class SourceGeneratorsNestedRecursiveSuppressorTests
{
    [Fact]
    public async Task NestedBoolLeaf_AllValuesCovered_HasNoSurvivingCompilerWarning()
        => await AssertNoSurvivingWarning(NestedRecursiveSources.NestedBoolComplete);

    [Fact]
    public async Task NestedEnumLeaf_MissingMember_RemainsCompilerWarning()
        => await AssertWarningRemains(NestedRecursiveSources.NestedEnumMissing);

    [Fact]
    public async Task NullableParent_NullAndNestedBoolCovered_HasNoSurvivingCompilerWarning()
        => await AssertNoSurvivingWarning(NestedRecursiveSources.NullableParentComplete);

    [Fact]
    public async Task NullableRoot_NullAndNestedLeafCovered_HasNoSurvivingCompilerWarning()
        => await AssertNoSurvivingWarning(NestedRecursiveSources.NullableRootComplete);

    [Fact]
    public async Task SameLeafSymbolOnIndependentPaths_RemainsCompilerWarning()
        => await AssertWarningRemains(NestedRecursiveSources.SiblingMissing);

    [Fact]
    public async Task CorrelatedEnumAndBoolLeaves_MissingValidPair_RemainsCompilerWarning()
        => await AssertWarningRemains(NestedRecursiveSources.CorrelatedMissing);

    [Fact]
    public async Task DerivedTypeParent_NestedLeavesRemainCorrelated()
        => await AssertWarningRemains(NestedRecursiveSources.DerivedMissing);

    [Fact]
    public async Task DerivedTypeGuard_DoesNotProveNestedCoverage()
        => await AssertWarningRemains(NestedRecursiveSources.DerivedGuardMissing);

    [Fact]
    public async Task UnsupportedUserDefinedDeconstruction_IsNeverSuppressed()
        => await AssertWarningRemains(NestedRecursiveSources.UnsupportedDeconstruction);

    [Fact]
    public async Task UnsupportedUserDefinedDeconstruction_WithDiscardHasNoSurvivingWarning()
        => await AssertNoSurvivingWarning(
            NestedRecursiveSources.UnsupportedDeconstructionWithDiscardComplete);

    [Fact]
    public async Task ManualDiscriminatedUnionDeconstructionMissingPayload_RemainsCompilerWarning()
        => await AssertWarningRemains(
            NestedRecursiveSources.ManualStructUnionDeconstructionMissing);

    [Fact]
    public async Task ManualNullableDiscriminatedUnionDeconstructionMissingPayload_RemainsCompilerWarning()
        => await AssertWarningRemains(
            NestedRecursiveSources.ManualNullableStructUnionDeconstructionMissing);

    [Fact]
    public async Task NegatedEnumComplement_RemainsCompilerWarning()
        => await AssertWarningRemains(NestedRecursiveSources.NegatedEnumComplementMissing);

    [Fact]
    public async Task ExplicitDiscardWildcard_HasNoSurvivingCompilerWarning()
        => await AssertNoSurvivingWarning(NestedRecursiveSources.ExplicitDiscard);

    [Fact]
    public async Task GuardedArm_DoesNotProveNestedBoolCoverage()
        => await AssertWarningRemains(NestedRecursiveSources.GuardedNested);

    [Fact]
    public async Task NumericBoolCorrelation_DoubleLeavesFiniteHoles()
    {
        const string source = NestedRecursiveSources.NumericBoolCorrelationDoubleMissing;
        await AssertWarningRemains(source);

        Assert.Equal(1, await InvokeIntAsync(source, "Read", 4d, true));
        Assert.Equal(2, await InvokeIntAsync(source, "Read", 4d, false));
        await AssertSwitchExpressionThrowsAsync(source, 0d, false);
        await AssertSwitchExpressionThrowsAsync(source, 6d, true);
    }

    [Fact]
    public async Task NumericBoolCorrelation_FloatLeavesFiniteHoles()
    {
        const string source = NestedRecursiveSources.NumericBoolCorrelationFloatMissing;
        await AssertWarningRemains(source);

        Assert.Equal(1, await InvokeIntAsync(source, "Read", 4f, true));
        Assert.Equal(2, await InvokeIntAsync(source, "Read", 4f, false));
        await AssertSwitchExpressionThrowsAsync(source, 0f, false);
        await AssertSwitchExpressionThrowsAsync(source, 6f, true);
    }

    [Fact]
    public async Task ExtendedPropertySiblingSharedLeaf_RemainsCompilerWarning()
        => await AssertWarningRemains(
            NestedRecursiveSources.ExtendedPropertySiblingSharedLeafMissing);

    [Fact]
    public async Task ExtendedPropertyNullableSibling_RemainsCompilerWarning()
        => await AssertWarningRemains(
            NestedRecursiveSources.ExtendedPropertyNullableSiblingMissing);

    [Fact]
    public async Task NestedStringLiteralRestriction_RemainsCompilerWarning()
        => await AssertWarningRemains(NestedRecursiveSources.NestedStringLiteralMissing);

    [Fact]
    public async Task GeneratedStructCommand_OrWrappedPayloadRestriction_RemainsCompilerWarning()
        => await AssertGeneratedWarningRemains(
            NestedRecursiveSources.GeneratedStructCommandOrWrappedMissing);

    [Fact]
    public async Task GeneratedStructCommand_OrWrappedPayloadRestrictionWithDiscardIsComplete()
        => await AssertGeneratedComplete(
            NestedRecursiveSources.GeneratedStructCommandOrWrappedDiscardComplete);

    [Fact]
    public async Task EnforcedHierarchy_PositionalDeconstructionRestriction_RemainsCompilerWarning()
        => await AssertWarningRemains(
            NestedRecursiveSources.EnforcedHierarchyPositionalDeconstructMissing);

    [Fact]
    public async Task EnforcedHierarchy_PositionalAndPropertyRestrictionDoesNotPoisonDiscardProof()
        => await AssertWarningRemains(
            NestedRecursiveSources.EnforcedHierarchyPositionalAndPropertyComplete);

    [Fact]
    public async Task EmitLoadExecute_BoolNullableAndSiblingFiniteMatrix()
    {
        const string source = NestedRecursiveSources.RuntimeBoolNullableSiblingComplete;
        await AssertNoSurvivingWarning(source);

        Assert.Equal(0, await InvokeIntAsync(source, "Nullable", new object?[] { null }));
        Assert.Equal(1, await InvokeIntAsync(source, "Nullable", true));
        Assert.Equal(2, await InvokeIntAsync(source, "Nullable", false));
        Assert.Equal(11, await InvokeIntAsync(source, "Sibling", true, true));
        Assert.Equal(12, await InvokeIntAsync(source, "Sibling", true, false));
        Assert.Equal(13, await InvokeIntAsync(source, "Sibling", false, true));
        Assert.Equal(14, await InvokeIntAsync(source, "Sibling", false, false));
    }

    [Fact]
    public async Task EmitLoadExecute_EnumNestedFiniteMatrix()
    {
        const string source = NestedRecursiveSources.RuntimeEnumDerivedComplete;
        await AssertNoSurvivingWarning(source);

        Assert.Equal(1, await InvokeIntAsync(source, "M", 0));
        Assert.Equal(2, await InvokeIntAsync(source, "M", 1));
        Assert.Equal(3, await InvokeIntAsync(source, "M", 2));
        Assert.Equal(11, await InvokeIntAsync(source, "D", 0, true));
        Assert.Equal(12, await InvokeIntAsync(source, "D", 0, false));
        Assert.Equal(13, await InvokeIntAsync(source, "D", 1, true));
        Assert.Equal(14, await InvokeIntAsync(source, "D", 1, false));
    }

    [Fact]
    public async Task MissingNullAndMissingBoolRemainWarnings_AndRestoredNullHasNoSurvivingWarning()
    {
        await AssertWarningRemains(NestedRecursiveSources.MissingNullableParent);
        await AssertWarningRemains(NestedRecursiveSources.MissingBool);
        await AssertNoSurvivingWarning(NestedRecursiveSources.RestoredNullableParentComplete);
    }

    [Fact]
    public async Task GeneratedStructCommand_MissingNestedFlagReportsSPIRE009AndLeavesCompilerWarning()
        => await AssertGeneratedMissing(NestedRecursiveSources.GeneratedStructCommandMissing);

    [Fact]
    public async Task GeneratedStructCommand_ComplementRestoresCompleteCoverage()
        => await AssertGeneratedComplete(NestedRecursiveSources.GeneratedStructCommandComplete);

    [Fact]
    public async Task GeneratedCommand_PayloadEnumMembersAreNotReportedAsVariants()
        => await AssertGeneratedMissingVariants(
            NestedRecursiveSources.GeneratedCommandPayloadEnumMissing,
            expectedMissingVariants: "A");

    [Fact]
    public async Task GeneratedCommand_MultiplePayloadGapsDeduplicateVariantName()
        => await AssertGeneratedMissingVariants(
            NestedRecursiveSources.GeneratedCommandPayloadBoolManyMissing,
            expectedMissingVariants: "A");

    [Fact]
    public async Task GeneratedNullableCommand_MissingFlagAndNullPathReportsSPIRE009()
        => await AssertGeneratedMissing(NestedRecursiveSources.GeneratedNullableCommandMissing);

    [Fact]
    public async Task GeneratedNullableCommand_MissingNullPathReportsSPIRE009AndCS8524()
        => await AssertGeneratedMissingWithCompilerId(
            NestedRecursiveSources.GeneratedNullableCommandMissingNull,
            expectedCompilerId: "CS8524");

    [Fact]
    public async Task NullableRoot_MissingNullPathReportsCS8655AndRemainsUnsuppressed()
        => await AssertWarningWithCompilerId(
            NestedRecursiveSources.NullableRootMissingNull,
            expectedCompilerId: "CS8655");

    [Fact]
    public async Task GeneratedNullableCommand_ComplementRestoresCompleteCoverage()
        => await AssertGeneratedComplete(NestedRecursiveSources.GeneratedNullableCommandComplete);

    [Fact]
    public async Task GeneratedRecordCommand_MissingNestedBoolReportsSPIRE009()
        => await AssertGeneratedMissing(NestedRecursiveSources.GeneratedRecordCommandMissing);

    [Fact]
    public async Task GeneratedRecordCommand_ComplementRestoresCompleteCoverage()
        => await AssertGeneratedComplete(NestedRecursiveSources.GeneratedRecordCommandComplete);

    private static async Task AssertNoSurvivingWarning(string source)
    {
        var (_, originalDiagnostics, diagnostics) = await Analyze(source);
        var originalWarnings = originalDiagnostics
            .Where(IsExhaustivenessDiagnostic)
            .ToArray();
        var survivingWarnings = diagnostics
            .Where(d => IsExhaustivenessDiagnostic(d) && !d.IsSuppressed)
            .ToArray();

        Assert.Empty(survivingWarnings);

        // A complete property/bool switch may already be accepted by Roslyn.
        // Require suppression metadata only when the original compiler emitted
        // a warning for that exact source location.
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics, d => IsSameDiagnostic(d, original) && d.IsSuppressed);
        }
    }

    private static async Task AssertWarningRemains(string source)
    {
        var (_, originalDiagnostics, diagnostics) = await Analyze(source);
        var originalWarnings = originalDiagnostics
            .Where(IsExhaustivenessDiagnostic)
            .ToArray();

        // Missing and unsupported cases must exercise the compiler warning
        // path; a test cannot pass merely because no diagnostic was produced.
        Assert.NotEmpty(originalWarnings);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics,
                d => IsSameDiagnostic(d, original) && !d.IsSuppressed);
        }
    }

    private static async Task AssertWarningWithCompilerId(
        string source,
        string expectedCompilerId)
    {
        var (_, originalDiagnostics, diagnostics) = await Analyze(source);
        var originalWarnings = originalDiagnostics
            .Where(d => d.Id == expectedCompilerId)
            .ToArray();

        Assert.True(originalWarnings.Length > 0,
            $"Expected {expectedCompilerId}; original diagnostics: " +
            string.Join(" | ", originalDiagnostics.Select(d => $"{d.Id}:{d.GetMessage()}")));
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics,
                d => IsSameDiagnostic(d, original) && !d.IsSuppressed);
        }
    }

    private static bool IsExhaustivenessDiagnostic(RoslynDiagnostic diagnostic)
        => diagnostic.Id is "CS8509" or "CS8524" or "CS8655";

    private static bool IsSameDiagnostic(RoslynDiagnostic left, RoslynDiagnostic right)
        => left.Id == right.Id &&
           string.Equals(
               left.Location.SourceTree?.FilePath,
               right.Location.SourceTree?.FilePath,
               StringComparison.Ordinal) &&
           left.Location.SourceSpan.Equals(right.Location.SourceSpan);

    private static bool IsUserSourceDiagnostic(RoslynDiagnostic diagnostic)
        => diagnostic.Location.SourceTree?.FilePath == "generated-nested-case.cs";

    private static async Task AssertGeneratedMissing(string source)
    {
        _ = await AssertGeneratedMissingCore(source);
    }

    private static async Task AssertGeneratedWarningRemains(string source)
    {
        var (_, originalDiagnostics, diagnostics) = await AnalyzeGenerated(source);
        var originalWarnings = originalDiagnostics
            .Where(d => IsExhaustivenessDiagnostic(d) && IsUserSourceDiagnostic(d))
            .ToArray();
        Assert.NotEmpty(originalWarnings);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics,
                d => IsSameDiagnostic(d, original) && !d.IsSuppressed);
        }
    }

    private static async Task AssertGeneratedMissingVariants(
        string source,
        string expectedMissingVariants)
    {
        var diagnostic = await AssertGeneratedMissingCore(source);
        Assert.Equal(
            expectedMissingVariants,
            diagnostic.Properties["MissingVariants"]);
    }

    private static async Task AssertGeneratedMissingWithCompilerId(
        string source,
        string expectedCompilerId)
    {
        var (_, originalDiagnostics, diagnostics) = await AnalyzeGenerated(source);
        Assert.Contains(diagnostics,
            d => d.Id == "SPIRE009" && IsUserSourceDiagnostic(d));

        var originalWarnings = originalDiagnostics
            .Where(d => d.Id == expectedCompilerId && IsUserSourceDiagnostic(d))
            .ToArray();
        Assert.True(originalWarnings.Length > 0,
            $"Expected {expectedCompilerId}; original diagnostics: " +
            string.Join(" | ", originalDiagnostics.Select(d => $"{d.Id}:{d.GetMessage()}")));
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics,
                d => IsSameDiagnostic(d, original) && !d.IsSuppressed);
        }
    }

    private static async Task<RoslynDiagnostic> AssertGeneratedMissingCore(string source)
    {
        var (_, originalDiagnostics, diagnostics) = await AnalyzeGenerated(source);
        var diagnostic = Assert.Single(diagnostics.Where(
            d => d.Id == "SPIRE009" && IsUserSourceDiagnostic(d)));

        var originalWarnings = originalDiagnostics
            .Where(d => IsExhaustivenessDiagnostic(d) && IsUserSourceDiagnostic(d))
            .ToArray();
        Assert.NotEmpty(originalWarnings);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics,
                d => IsSameDiagnostic(d, original) && !d.IsSuppressed);
        }

        return diagnostic;
    }

    private static async Task AssertGeneratedComplete(string source)
    {
        var (_, originalDiagnostics, diagnostics) = await AnalyzeGenerated(source);
        Assert.DoesNotContain(diagnostics,
            d => d.Id == "SPIRE009" && IsUserSourceDiagnostic(d));

        var originalWarnings = originalDiagnostics
            .Where(d => IsExhaustivenessDiagnostic(d) && IsUserSourceDiagnostic(d))
            .ToArray();
        var survivingWarnings = diagnostics
            .Where(d => IsExhaustivenessDiagnostic(d) && IsUserSourceDiagnostic(d)
                && !d.IsSuppressed)
            .ToArray();
        Assert.Empty(survivingWarnings);
        foreach (var original in originalWarnings)
        {
            Assert.Contains(diagnostics,
                d => IsSameDiagnostic(d, original) && d.IsSuppressed);
        }
    }

    private static async Task<(
        CSharpCompilation Compilation,
        ImmutableArray<RoslynDiagnostic> OriginalDiagnostics,
        ImmutableArray<RoslynDiagnostic> Diagnostics)> Analyze(string source)
    {
        var refs = TestHarnessEnvironment.FrameworkReferences
            .Add(MetadataReference.CreateFromFile(
                typeof(EnforceExhaustivenessAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(
                typeof(CS8509Suppressor).Assembly.Location));
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "nested-case.cs");
        var trees = ImmutableArray.Create<SyntaxTree>(tree);
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(parseOptions, refs);
        if (compatibilityTree is not null)
            trees = trees.Add(compatibilityTree);

        var compilation = CSharpCompilation.Create(
            $"SourceGeneratorsNestedRecursiveSuppressorTests_{Guid.NewGuid():N}",
            trees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var originalDiagnostics = compilation.GetDiagnostics();
        var errors = originalDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(errors.Length == 0,
            "Test source must compile without errors:\n" +
            string.Join("\n", errors.Select(d => $"{d.Id}: {d.GetMessage()}")));

        var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new CS8509Suppressor()),
            new CompilationWithAnalyzersOptions(
                analyzerOptions,
                onAnalyzerException: null,
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true));
        var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return (compilation, originalDiagnostics, diagnostics);
    }

    private static async Task<(
        Compilation Compilation,
        ImmutableArray<RoslynDiagnostic> OriginalDiagnostics,
        ImmutableArray<RoslynDiagnostic> Diagnostics)> AnalyzeGenerated(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "generated-nested-case.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        var originalDiagnostics = outputCompilation.GetDiagnostics();
        var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        var withAnalyzers = outputCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(
                new ExhaustivenessAnalyzer(),
                new CS8509Suppressor()),
            new CompilationWithAnalyzersOptions(
                analyzerOptions,
                onAnalyzerException: null,
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true));
        var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return (outputCompilation, originalDiagnostics, diagnostics);
    }

    private static async Task<int> InvokeIntAsync(
        string source,
        string methodName,
        params object?[] args)
    {
        var (compilation, _, _) = await Analyze(source);
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success,
            string.Join("\n", emit.Diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));
        var assembly = TestHarnessEnvironment.LoadAssembly(stream.ToArray());
        var method = assembly.GetType("C", throwOnError: true)!.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return (int)method!.Invoke(null, args)!;
    }

    private static async Task AssertSwitchExpressionThrowsAsync(
        string source,
        params object?[] args)
    {
        var exception = await Assert.ThrowsAsync<TargetInvocationException>(
            () => InvokeIntAsync(source, "Read", args));
        Assert.Equal(
            "System.Runtime.CompilerServices.SwitchExpressionException",
            exception.InnerException?.GetType().FullName);
    }
}
