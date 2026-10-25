using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.DiagnosticContinuity;

/// Regression coverage for the rule that a generator diagnostic must not hide
/// unrelated generated API that remains constructible.
public sealed class DiagnosticContinuityTests
{
    [Fact]
    public void RecordFieldConflict_RetainsIndependentRecordSurface()
    {
        GeneratorTestHelper.RunGenerator(
            RecordFieldConflictWithConsumerSource,
            out var compilation,
            out var diagnostics,
            path: "record-field-conflict.cs");

        var diagnostic = Assert.Single(diagnostics.Where(item => item.Id == "SPIRE_DU010"));
        Assert.Contains("value", diagnostic.GetMessage(), StringComparison.Ordinal);

        AssertNoErrors(compilation);
        var union = Require(compilation.GetTypeByMetadataName("RecordFieldConflictUnion"));
        Assert.NotEmpty(union.GetMembers("IsCount"));
        Assert.NotEmpty(union.GetMembers("IsText"));
    }

    [Fact]
    public void InvalidFormerlySerializedAsAlias_DoesNotCorruptCanonicalPayloads()
    {
        var result = UnityGeneratorTestHarness.Run(InvalidAliasSource);

        Assert.Contains(result.GeneratorDiagnostics, item => item.Id == "SPIRE_DU017");
        AssertNoErrors(result.OutputCompilation);

        var union = Require(
            result.OutputCompilation.GetTypeByMetadataName("InvalidAliasUnion"));
        Assert.NotEmpty(union.GetMembers("First"));
        Assert.NotEmpty(union.GetMembers("Second"));
    }

    [Fact]
    public void UnsupportedNonPartialClass_ReportsPrimaryDiagnosticWithoutPartialCascade()
    {
        GeneratorTestHelper.RunGenerator(
            UnsupportedNonPartialClassSource,
            out var compilation,
            out var diagnostics,
            path: "unsupported-class.cs");

        Assert.Contains(diagnostics, item => item.Id == "SPIRE_DU011");
        Assert.DoesNotContain(
            compilation.GetDiagnostics(),
            item => item.Id is "CS0260" or "CS0759" or "CS0101");
        Assert.Empty(compilation.SyntaxTrees.Skip(1));
    }

    [Fact]
    public void MissingUnityReferences_RetainsCoreUnionOutput()
    {
        GeneratorTestHelper.RunGenerator(
            MissingUnityReferencesSource,
            out var compilation,
            out var diagnostics,
            path: "missing-unity-references.cs");

        Assert.Contains(diagnostics, item => item.Id == "SPIRE_DU016");
        AssertNoErrors(compilation);
        var union = Require(compilation.GetTypeByMetadataName("MissingUnityReferencesUnion"));
        Assert.NotEmpty(union.GetMembers("Value"));
    }

    [Fact]
    public void InlinableInvalidArity_LeavesInvalidDelegateOrdinaryAndKeepsValidSibling()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableInvalidArityWithValidSiblingSource,
            out var compilation,
            out _,
            path: "inlinable-invalid-arity.cs");

        var diagnostics = RunInlinableAnalyzer(compilation);
        Assert.Contains(diagnostics, item => item.Id == "SPIRE024");

        var generated = Require(GeneratorTestHelper.GetInlinableGeneratedSource(result));
        Assert.True(
            generated.Contains("Action<int, int, int, int, int, int, int, int, int> tooWide", StringComparison.Ordinal),
            generated);
        Assert.Contains("IActionInliner<int>", generated);
        Assert.DoesNotContain("IActionInliner<int, int, int, int, int, int, int, int, int>", generated);
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinableRefParameter_LeavesInvalidDelegateOrdinaryAndKeepsValidSibling()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableRefWithValidSiblingSource,
            out var compilation,
            out _,
            path: "inlinable-invalid-ref.cs");

        var diagnostics = RunInlinableAnalyzer(compilation);
        Assert.Contains(diagnostics, item => item.Id == "SPIRE025");

        var generated = Require(GeneratorTestHelper.GetInlinableGeneratedSource(result));
        Assert.True(generated.Contains("ref Action invalid", StringComparison.Ordinal), generated);
        Assert.Contains("IActionInliner<int>", generated);
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinableNonPartialContainer_ReportsDiagnosticWithoutBogusTwin()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableNonPartialContainerSource,
            out var compilation,
            out _,
            path: "inlinable-nonpartial.cs");

        var diagnostics = RunInlinableAnalyzerOnSource(InlinableNonPartialContainerSource);
        Assert.Contains(diagnostics, item => item.Id == "SPIRE023");
        Assert.Null(GeneratorTestHelper.GetInlinableGeneratedSource(result));
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinableBodyDiagnostic_PreservesUsableSiblingTwin()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableBodyDiagnosticWithValidSiblingSource,
            out var compilation,
            out _,
            path: "inlinable-body-diagnostic.cs");

        var diagnostics = RunInlinableAnalyzer(compilation);
        Assert.Contains(diagnostics, item => item.Id == "SPIRE021");

        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedTrees
                .Where(tree => tree.FilePath.EndsWith(".InlinableTwin.g.cs", StringComparison.Ordinal))
                .Select(tree => tree.GetText().ToString()));
        Assert.NotEmpty(generated);
        Assert.Contains("IActionInliner<int>", generated);
        Assert.DoesNotContain("Invalid<", generated, StringComparison.Ordinal);
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinableArityEightPartialContainer_RemainsConstructible()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableArityEightSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "inlinable-arity-eight.cs");

        Assert.Empty(generatorDiagnostics);
        var generated = Require(GeneratorTestHelper.GetInlinableGeneratedSource(result));
        Assert.Contains(
            "IActionInliner<int, int, int, int, int, int, int, int>",
            generated);
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinerStructNonPartialContainer_ReportsDiagnosticWithoutBrokenSource()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            InlinerStructNonPartialSource,
            out var compilation,
            out var diagnostics,
            path: "inliner-struct-nonpartial.cs");

        Assert.Contains(diagnostics, item => item.Id == "SPIRE027");
        Assert.Null(GeneratorTestHelper.GetInlinerGeneratedSource(result));
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinerStructArityNine_ReportsDiagnosticWithoutMissingInterfaceError()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            InlinerStructArityNineSource,
            out var compilation,
            out var diagnostics,
            path: "inliner-struct-arity-nine.cs");

        Assert.Contains(diagnostics, item => item.Id == "SPIRE019");
        Assert.Null(GeneratorTestHelper.GetInlinerGeneratedSource(result));
        AssertNoErrors(compilation);
    }

    [Fact]
    public void InlinerStructArityEightPartialContainer_RemainsConstructible()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            InlinerStructArityEightSource,
            out var compilation,
            out var diagnostics,
            path: "inliner-struct-arity-eight.cs");

        Assert.Empty(diagnostics);
        Assert.Contains(
            "IActionInliner<int, int, int, int, int, int, int, int>",
            Require(GeneratorTestHelper.GetInlinerGeneratedSource(result)));
        AssertNoErrors(compilation);
    }

    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> RunInlinableAnalyzer(
        Compilation compilation)
        => compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new InlinableUsageAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> RunInlinableAnalyzerOnSource(
        string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "standalone-inlinable.cs");
        var references = TestHarnessEnvironment.RuntimeReferences
            .Append(MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "StandaloneInlinable",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return RunInlinableAnalyzer(compilation);
    }

    private static T Require<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value!;
    }

    private static void AssertNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(item => item.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(item =>
                $"{item.Id}: {item.GetMessage()}")));
    }

    private const string RecordFieldConflictWithConsumerSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record RecordFieldConflictUnion
        {
            public partial record Count(int value) : RecordFieldConflictUnion;
            public partial record Text(string value) : RecordFieldConflictUnion;
        }

        public static class RecordFieldConflictConsumer
        {
            public static bool IsKnown(RecordFieldConflictUnion value)
                => value.IsCount || value.IsText;
        }
        """;

    private const string InvalidAliasSource = """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public partial struct InvalidAliasUnion
        {
            [Variant(FormerlySerializedAs = "Second")]
            public static partial InvalidAliasUnion First(int value);
            [Variant]
            public static partial InvalidAliasUnion Second(int other);
        }
        """;

    private const string UnsupportedNonPartialClassSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public class UnsupportedNonPartialUnion
        {
            public sealed class Start : UnsupportedNonPartialUnion { }
        }
        """;

    private const string MissingUnityReferencesSource = """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public partial struct MissingUnityReferencesUnion
        {
            [Variant]
            public static partial MissingUnityReferencesUnion Value(int value);
        }
        """;

    private const string InlinableInvalidArityWithValidSiblingSource = """
        using System;
        using Houtamelo.Spire;

        public static partial class InlinableArityHost
        {
            public static void Run(
                [Inlinable] Action<int, int, int, int, int, int, int, int, int> tooWide,
                [Inlinable] Action<int> valid,
                int value)
            {
                tooWide(value, value, value, value, value, value, value, value, value);
                valid(value);
            }
        }
        """;

    private const string InlinableRefWithValidSiblingSource = """
        using System;
        using Houtamelo.Spire;

        public static partial class InlinableRefHost
        {
            public static void Run(
                [Inlinable] ref Action invalid,
                [Inlinable] Action<int> valid,
                int value)
            {
                invalid();
                valid(value);
            }
        }
        """;

    private const string InlinableNonPartialContainerSource = """
        using System;
        using Houtamelo.Spire;

        public static class InlinableNonPartialHost
        {
            public static void Run([Inlinable] Action callback)
            {
                callback();
            }
        }
        """;

    private const string InlinableBodyDiagnosticWithValidSiblingSource = """
        using System;
        using Houtamelo.Spire;

        public static partial class InlinableBodyHost
        {
            public static Action Invalid([Inlinable] Action returned)
                => returned;

            public static void Valid([Inlinable] Action<int> valid, int value)
                => valid(value);
        }
        """;

    private const string InlinableArityEightSource = """
        using System;
        using Houtamelo.Spire;

        public static partial class InlinableEightHost
        {
            public static void Run(
                [Inlinable] Action<int, int, int, int, int, int, int, int> callback,
                int a, int b, int c, int d, int e, int f, int g, int h)
                => callback(a, b, c, d, e, f, g, h);
        }
        """;

    private const string InlinerStructNonPartialSource = """
        using Houtamelo.Spire;

        public static class InlinerStructNonPartialHost
        {
            [InlinerStruct]
            public static void Run(int value) { }
        }
        """;

    private const string InlinerStructArityNineSource = """
        using Houtamelo.Spire;

        public static partial class InlinerStructWideHost
        {
            [InlinerStruct]
            public static void Run(
                int a, int b, int c, int d, int e, int f, int g, int h, int i) { }
        }
        """;

    private const string InlinerStructArityEightSource = """
        using Houtamelo.Spire;

        public static partial class InlinerStructEightHost
        {
            [InlinerStruct]
            public static void Run(
                int a, int b, int c, int d, int e, int f, int g, int h) { }
        }
        """;
}
