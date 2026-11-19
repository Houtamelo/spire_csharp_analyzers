using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Regression coverage for nullable annotations on generated reference storage.
[Trait("Environment", "Unity")]
public sealed class GeneratedStringStorageNullabilityTests
{
    [Theory]
    [InlineData("Typed")]
    [InlineData("Auto")]
    [InlineData("Shared")]
    public void AuthoredRecordStringFields_AreWarningFreeWithPortableReferences(string storage)
    {
        var result = UnityGeneratorTestHarness.Run(
            AuthoredRecordSource(storage, nullableFirst: true),
            includeUnityReferences: true);

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        AssertNoWarningsInGeneratedTrees(result.OutputCompilation, result.RunResult.GeneratedTrees);
        AssertStringAnnotations(result.OutputCompilation, "SerializedStringKeys");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AuthoredRecordStringFields_PreserveAnnotationsInEitherDeclarationOrder(bool nullableFirst)
    {
        var result = UnityGeneratorTestHarness.Run(
            AuthoredRecordSource("Typed", nullableFirst),
            includeUnityReferences: true);

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        AssertNoWarningsInGeneratedTrees(result.OutputCompilation, result.RunResult.GeneratedTrees);
        AssertStringAnnotations(result.OutputCompilation, "SerializedStringKeys");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AuthoredRecordStringFields_AreWarningFreeWithUnityBclReferences(bool nullableFirst)
    {
        var result = RunWithUnityBclReferences(AuthoredRecordSource("Typed", nullableFirst));

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        AssertNoWarningsInGeneratedTrees(result.OutputCompilation, result.GeneratedTrees);
        AssertStringAnnotations(result.OutputCompilation, "SerializedStringKeys");
        AssertUnityComparerBoundary(result.OutputCompilation);
    }

    [Fact]
    public void FactoryStringFields_AreWarningFreeWithUnityReferences()
    {
        var result = RunWithUnityBclReferences(FactoryStringSource());

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        AssertNoWarningsInGeneratedTrees(result.OutputCompilation, result.GeneratedTrees);
    }

    [Fact]
    public void MixedFactoryAndAuthoredRecordStringFields_AreWarningFree()
    {
        var result = RunWithUnityBclReferences(MixedFactoryAndRecordSource());

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        AssertNoWarningsInGeneratedTrees(result.OutputCompilation, result.GeneratedTrees);
    }

    [Fact]
    public void NestedGenericRecordFields_AreWarningFreeWithUnityBclReferences()
    {
        var result = RunWithUnityBclReferences(NestedGenericRecordSource());

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        AssertNoWarningsInGeneratedTrees(result.OutputCompilation, result.GeneratedTrees);
    }

    [Fact]
    public void AuthoredRecordStringFields_PreserveNullEqualityAndPayloadOperators()
    {
        var result = UnityGeneratorTestHarness.Run(
            AuthoredRecordSource("Typed", nullableFirst: true) + RuntimeProbeSource(),
            includeUnityReferences: true);

        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result.OutputCompilation);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "generated nullable string equality");
        var probe = assembly.GetType("GeneratedStringStorageProbe", throwOnError: true)!;
        var failures = (string)probe.GetMethod("CheckFailures")!.Invoke(null, null)!;
        Assert.Equal(string.Empty, failures);
    }

    private static void AssertNoGeneratorDiagnostics(UnityGeneratorRun result)
    {
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.True(
            diagnostics.Length == 0,
            "Unexpected generator diagnostics:" + Environment.NewLine + string.Join(Environment.NewLine, diagnostics));
    }

    private static void AssertNoGeneratorDiagnostics(UnityBclGeneratorRun result)
    {
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.True(
            diagnostics.Length == 0,
            "Unexpected generator diagnostics:" + Environment.NewLine + string.Join(Environment.NewLine, diagnostics));
    }

    private static void AssertNoWarningsInGeneratedTrees(
        Compilation compilation,
        IEnumerable<SyntaxTree> generatedTrees)
    {
        var generatedPaths = generatedTrees
            .Select(tree => tree.FilePath)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToHashSet(StringComparer.Ordinal);
        var warnings = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Where(diagnostic => diagnostic.Location.SourceTree is { } tree &&
                generatedPaths.Contains(tree.FilePath))
            .ToArray();
        Assert.True(
            warnings.Length == 0,
            "Unexpected generated nullable diagnostics:" + Environment.NewLine +
            string.Join(Environment.NewLine, warnings.Select(diagnostic => diagnostic.ToString())));
    }

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            "Unexpected compilation errors:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString())));
    }

    private static void AssertUnityComparerBoundary(Compilation compilation)
    {
        var comparerDefinition = compilation.GetTypeByMetadataName(
            "System.Collections.Generic.EqualityComparer`1");
        Assert.NotNull(comparerDefinition);
        var comparer = comparerDefinition!.Construct(compilation.GetSpecialType(SpecialType.System_String));
        var equals = Assert.Single(comparer.GetMembers("Equals")
            .OfType<IMethodSymbol>()
, method => method.Parameters.Length == 2);
        Assert.All(equals.Parameters, parameter =>
            Assert.True(
                parameter.NullableAnnotation is NullableAnnotation.None or NullableAnnotation.NotAnnotated,
                $"Unity comparer parameter '{parameter.Name}' unexpectedly has nullable annotation {parameter.NullableAnnotation}."));
    }

    private static void AssertStringAnnotations(Compilation compilation, string unionName)
    {
        var optional = compilation.GetTypeByMetadataName(unionName + "+Optional")!;
        var required = compilation.GetTypeByMetadataName(unionName + "+Required")!;
        var irisShape = compilation.GetTypeByMetadataName(unionName + "+IrisShape")!;
        var penis = compilation.GetTypeByMetadataName(unionName + "+Penis")!;

        Assert.Equal(
            NullableAnnotation.Annotated,
            Assert.Single(optional.GetMembers("key").OfType<IPropertySymbol>()).NullableAnnotation);
        foreach (var payload in new[] { required, irisShape, penis })
        {
            Assert.Equal(
                NullableAnnotation.NotAnnotated,
                Assert.Single(payload.GetMembers("key").OfType<IPropertySymbol>()).NullableAnnotation);
        }
    }

    private static UnityBclGeneratorRun RunWithUnityBclReferences(string source)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("UNITY_5_3_OR_NEWER");
        var sourceTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "unity-bcl-nullability.cs");
        var references = UnityBclReferences().ToImmutableArray();
        var trees = new List<SyntaxTree> { sourceTree };
        var compatibility = AddIsExternalInitTree(parseOptions, references);
        if (compatibility is not null)
            trees.Add(compatibility);

        var compilation = CSharpCompilation.Create(
            "UnityBclNullability_" + Guid.NewGuid().ToString("N"),
            trees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        return new UnityBclGeneratorRun(
            outputCompilation,
            generatorDiagnostics,
            driver.GetRunResult().GeneratedTrees);
    }

    private static IEnumerable<MetadataReference> UnityBclReferences()
    {
        var apiRoot = Path.Combine(
            UnityGeneratorTestHarness.UnityEditorRoot,
            "Editor",
            "Data",
            "UnityReferenceAssemblies",
            "unity-4.8-api");
        var facadeRoot = Path.Combine(apiRoot, "Facades");
        var paths = Directory.EnumerateFiles(apiRoot, "*.dll")
            .Concat(Directory.EnumerateFiles(facadeRoot, "*.dll"))
            .Concat(Directory.EnumerateFiles(UnityGeneratorTestHarness.UnityEngineRoot, "UnityEngine*.dll"))
            .Append(UnityGeneratorTestHarness.UnityScriptingAssemblyPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Append(typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location);
        return paths.Select(path => MetadataReference.CreateFromFile(path));
    }

    private static SyntaxTree? AddIsExternalInitTree(
        CSharpParseOptions parseOptions,
        ImmutableArray<MetadataReference> references)
    {
        var probe = CSharpCompilation.Create(
            "IsExternalInitProbe",
            new[] { CSharpSyntaxTree.ParseText("public sealed class Probe { }", parseOptions) },
            references);
        var init = probe.GetTypeByMetadataName("System.Runtime.CompilerServices.IsExternalInit");
        if (init is not null && probe.IsSymbolAccessibleWithin(init, probe.Assembly))
            return null;
        return CSharpSyntaxTree.ParseText(
            "namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }",
            parseOptions,
            path: "__SpireUnityBclIsExternalInit.cs");
    }

    private sealed class UnityBclGeneratorRun
    {
        internal UnityBclGeneratorRun(
            Compilation outputCompilation,
            ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> generatorDiagnostics,
            ImmutableArray<SyntaxTree> generatedTrees)
        {
            OutputCompilation = outputCompilation;
            GeneratorDiagnostics = generatorDiagnostics;
            GeneratedTrees = generatedTrees;
        }

        internal Compilation OutputCompilation { get; }
        internal ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> GeneratorDiagnostics { get; }
        internal ImmutableArray<SyntaxTree> GeneratedTrees { get; }
    }

    private static string AuthoredRecordSource(string storage, bool nullableFirst)
    {
        var variants = nullableFirst
            ? """
                [Variant] public partial record struct Optional(string? key);
                [Variant] public partial record struct Required(string key);
                """
            : """
                [Variant] public partial record struct Required(string key);
                [Variant] public partial record struct Optional(string? key);
                """;
        return $$"""
            #nullable enable
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable, DiscriminatedUnion(ReferenceStorage = ReferenceStorage.{{storage}})]
            public partial struct SerializedStringKeys
            {
                {{variants}}
                [Variant] public partial record struct IrisShape(int index, string key);
                [Variant] public partial record struct Penis(int kind, string key);
                [Variant] public partial record struct Empty;
            }
            """;
    }

    private static string FactoryStringSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct FactoryStringKeys
        {
            [Variant] public static partial FactoryStringKeys Optional(string? key);
            [Variant] public static partial FactoryStringKeys Required(string key);
            [Variant] public static partial FactoryStringKeys Empty();
        }
        """;

    private static string MixedFactoryAndRecordSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct MixedStringKeys
        {
            [Variant] public static partial MixedStringKeys Factory(string? key);
            [Variant] public partial record struct Record(string key);
            [Variant] public partial record struct Optional(string? key);
        }
        """;

    private static string NestedGenericRecordSource() => """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct NestedStringKeys
        {
            [Variant] public partial record struct NonNullable(List<string> values);
            [Variant] public partial record struct Nullable(List<string?> values);
        }
        """;

    private static string RuntimeProbeSource() => """
        public static class GeneratedStringStorageProbe
        {
            public static bool Check()
                => CheckFailures().Length == 0;

            public static string CheckFailures()
            {
                var optionalNull = new SerializedStringKeys.Optional(null);
                var optionalNullAgain = new SerializedStringKeys.Optional(null);
                var optionalValue = new SerializedStringKeys.Optional("value");
                var required = new SerializedStringKeys.Required("required");
                var requiredSame = new SerializedStringKeys.Required("required");
                var requiredDifferent = new SerializedStringKeys.Required("different");
                var iris = new SerializedStringKeys.IrisShape(1, "iris");
                var irisSame = new SerializedStringKeys.IrisShape(1, "iris");
                var irisDifferent = new SerializedStringKeys.IrisShape(2, "iris");
                var penis = new SerializedStringKeys.Penis(1, "penis");
                var penisSame = new SerializedStringKeys.Penis(1, "penis");
                var penisDifferent = new SerializedStringKeys.Penis(1, "other");
                SerializedStringKeys optionalUnion = optionalNull;
                SerializedStringKeys optionalUnionAgain = optionalNullAgain;
                SerializedStringKeys optionalValueUnion = optionalValue;
                SerializedStringKeys requiredUnion = required;
                SerializedStringKeys requiredSameUnion = requiredSame;
                SerializedStringKeys requiredDifferentUnion = requiredDifferent;
                SerializedStringKeys irisUnion = iris;
                SerializedStringKeys irisSameUnion = irisSame;
                SerializedStringKeys irisDifferentUnion = irisDifferent;
                SerializedStringKeys penisUnion = penis;
                SerializedStringKeys penisSameUnion = penisSame;
                SerializedStringKeys penisDifferentUnion = penisDifferent;
                var defaultUnion = default(SerializedStringKeys);
                var failures = new System.Collections.Generic.List<string>();
                if (!(optionalUnion.Equals(optionalUnionAgain) &&
                    optionalUnion.GetHashCode() == optionalUnionAgain.GetHashCode() &&
                    optionalUnion == optionalUnionAgain &&
                    optionalUnion == optionalNullAgain &&
                    optionalNullAgain == optionalUnion))
                    failures.Add("optional-null");
                if (!(defaultUnion.Equals(optionalUnion) &&
                    defaultUnion.GetHashCode() == optionalUnion.GetHashCode()))
                    failures.Add("default-null");
                if (!(optionalUnion != optionalValueUnion &&
                    optionalValueUnion != optionalUnion))
                    failures.Add("optional-different");
                if (!requiredUnion.Equals(requiredSameUnion))
                    failures.Add("required-typed");
                if (requiredUnion.GetHashCode() != requiredSameUnion.GetHashCode())
                    failures.Add("required-hash");
                if (!(requiredUnion == requiredSame))
                    failures.Add("required-union-payload");
                if (!(requiredSame == requiredUnion))
                    failures.Add("required-payload-union");
                if (!requiredUnion.Equals(requiredSame))
                    failures.Add("required-equals-payload");
                if (!(requiredUnion != requiredDifferentUnion &&
                    requiredUnion != optionalUnion))
                    failures.Add("required-different");
                if (!(irisUnion.Equals(irisSame) &&
                    irisUnion.Equals(irisSameUnion) &&
                    irisUnion == irisSame &&
                    irisSame == irisUnion &&
                    !(irisUnion != irisSame) &&
                    !(irisSame != irisUnion)))
                    failures.Add("iris-same");
                if (!(irisUnion != irisDifferentUnion &&
                    irisDifferent != irisUnion))
                    failures.Add("iris-different");
                if (!(penisUnion.Equals(penisSame) &&
                    penisUnion.Equals(penisSameUnion) &&
                    penisUnion == penisSame &&
                    penisSame == penisUnion &&
                    !(penisUnion != penisSame) &&
                    !(penisSame != penisUnion)))
                    failures.Add("penis-same");
                if (!(penisUnion != penisDifferentUnion &&
                    penisDifferent != penisUnion))
                    failures.Add("penis-different");
                return string.Join(",", failures);
            }
        }
        """;
}
