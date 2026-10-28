using System;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Covers the implicit payload-to-union conversions generated for public variants.
public sealed class VariantPayloadImplicitConversionTests
{
    [Fact]
    public void PublicVariants_ExposeExactOperatorsAndCompileAssignments()
    {
        var result = Generate(BasicSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Shape")!;
        var operators = ConversionOperators(union);
        Assert.Equal(new[] { "Circle", "Rectangle", "Point" },
            operators.Select(operatorSymbol => operatorSymbol.Parameters[0].Type.Name).ToArray());
        Assert.All(operators, operatorSymbol =>
        {
            Assert.Equal("op_Implicit", operatorSymbol.Name);
            Assert.Equal("Shape", operatorSymbol.ReturnType.Name);
            Assert.Equal(Accessibility.Public, operatorSymbol.DeclaredAccessibility);
            Assert.Equal(MethodKind.Conversion, operatorSymbol.MethodKind);
        });

    }

    [Fact]
    public void ManyVariants_EmitOneOperatorPerCanonicalPayload()
    {
        var result = Generate(ManyVariantsSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result);

        var operators = ConversionOperators(result.OutputCompilation.GetTypeByMetadataName("Many")!);
        Assert.Equal(8, operators.Length);
        Assert.Equal(
            new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
            operators.Select(operatorSymbol => operatorSymbol.Parameters[0].Type.Name).ToArray());
    }

    [Fact]
    public void GenericAndNestedUnions_PreserveTypeParametersAndConstraints()
    {
        var result = Generate(GenericAndNestedSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result);

        var generic = result.OutputCompilation.GetTypeByMetadataName("Result`2")!;
        var operators = ConversionOperators(generic);
        Assert.Equal(2, operators.Length);
        Assert.All(operators, operatorSymbol =>
        {
            Assert.Equal(2, operatorSymbol.ContainingType.TypeParameters.Length);
            Assert.Equal(2, ((INamedTypeSymbol)operatorSymbol.Parameters[0].Type).TypeParameters.Length);
            Assert.Equal("Result", operatorSymbol.ReturnType.Name);
        });

        var payload = result.OutputCompilation.GetTypeByMetadataName("ResultTypes2+Variants+Ok`2")!;
        Assert.Equal(2, payload.TypeParameters.Length);
        Assert.True(payload.TypeParameters[0].HasReferenceTypeConstraint);
        Assert.True(payload.TypeParameters[1].HasUnmanagedTypeConstraint);

        var nestedUnion = result.OutputCompilation.GetTypeByMetadataName("Outer+Command")!;
        Assert.Single(ConversionOperators(nestedUnion));
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("Outer+CommandTypes+Variants+Run"));
    }

    [Fact]
    public void ReadonlyAndRefStructUnions_CompilePayloadAssignments()
    {
        var readonlyResult = Generate(ReadonlySource());
        AssertNoGeneratorDiagnostics(readonlyResult);
        AssertNoCompilationErrors(readonlyResult);
        Assert.Single(ConversionOperators(
            readonlyResult.OutputCompilation.GetTypeByMetadataName("ReadonlyShape")!));

        foreach (var languageVersion in new[] { LanguageVersion.CSharp9, LanguageVersion.CSharp12 })
        {
            var refResult = Generate(RefStructSource(), languageVersion);
            AssertNoGeneratorDiagnostics(refResult);
            AssertNoCompilationErrors(refResult);
            Assert.Single(ConversionOperators(
                refResult.OutputCompilation.GetTypeByMetadataName("RefShape")!));
        }
    }

    [Fact]
    public void EscapedNullableAndTupleFields_PreservePayloadTypesAndLiftNullableConversion()
    {
        var result = Generate(EscapedNullableTupleSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Escaped")!;
        var operatorSymbol = Assert.Single(ConversionOperators(union));
        var payload = result.OutputCompilation.GetTypeByMetadataName("EscapedTypes+Variants+class")!;
        Assert.Equal(payload, operatorSymbol.Parameters[0].Type);
        Assert.Equal(2, payload.GetMembers().OfType<IPropertySymbol>().Count());
    }

    [Fact]
    public void NonPublicVariants_DoNotReceivePublicConversions()
    {
        var result = Generate(NonPublicSource());
        AssertNoGeneratorDiagnostics(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("AccessUnion")!;
        var operators = ConversionOperators(union);
        Assert.Single(operators);
        Assert.Equal("Visible", operators[0].Parameters[0].Type.Name);

        var conversionErrors = CompilationErrors(result.OutputCompilation)
            .Where(diagnostic => diagnostic.Id == "CS0029")
            .Select(diagnostic => diagnostic.GetMessage())
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, conversionErrors.Length);
        Assert.Equal(
            new[]
            {
                "Cannot implicitly convert type 'AccessUnionTypes.Variants.Hidden' to 'AccessUnion'",
                "Cannot implicitly convert type 'AccessUnionTypes.Variants.Secret' to 'AccessUnion'",
            }.OrderBy(message => message, StringComparer.Ordinal),
            conversionErrors);
    }

    [Fact]
    public void ConversionIsForwardOnly()
    {
        var result = Generate(ReverseConversionSource());
        AssertNoGeneratorDiagnostics(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("ForwardOnly")!;
        Assert.Single(ConversionOperators(union));
        var conversionErrors = CompilationErrors(result.OutputCompilation)
            .Where(diagnostic => diagnostic.Id == "CS0029")
            .Select(diagnostic => diagnostic.GetMessage())
            .ToArray();
        Assert.Equal(new[]
        {
            "Cannot implicitly convert type 'ForwardOnly' to 'ForwardOnlyTypes.Variants.Value'",
        }, conversionErrors);
    }

    [Fact]
    public void FormerNamePayload_IsCanonicalOnly()
    {
        var result = UnityGeneratorTestHarness.Run(
            FormerNameSource(),
            includeUnityReferences: true,
            includeEditorReferences: false);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Renamed")!;
        Assert.Single(ConversionOperators(union));

        var canonical = result.OutputCompilation.GetTypeByMetadataName("RenamedTypes+Variants+Current")!;
        var former = result.OutputCompilation.GetTypeByMetadataName("RenamedTypes+Variants+Old")!;
        Assert.Contains(ConversionOperators(union), operatorSymbol =>
            SymbolEqualityComparer.Default.Equals(operatorSymbol.Parameters[0].Type, canonical));
        Assert.Empty(ConversionOperators(former));

        var conversionErrors = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => diagnostic.Id == "CS0029")
            .Select(diagnostic => diagnostic.GetMessage())
            .ToArray();
        Assert.Equal(new[]
        {
            "Cannot implicitly convert type 'RenamedTypes.Variants.Old' to 'Renamed'",
        }, conversionErrors);
    }

    [Fact]
    public void EscapedUnionAndGenericParameterNamedVariant_DoNotShadowConversionParameters()
    {
        var result = Generate(ShadowingSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result);

        var escapedUnion = result.OutputCompilation.GetTypeByMetadataName("variant")!;
        var escapedOperator = Assert.Single(ConversionOperators(escapedUnion));
        Assert.Equal("Value", escapedOperator.Parameters[0].Type.Name);
        Assert.Equal("variant", escapedOperator.ReturnType.Name);

        var collisionUnion = result.OutputCompilation.GetTypeByMetadataName("Collision")!;
        var collisionOperator = Assert.Single(ConversionOperators(collisionUnion));
        Assert.Equal("variant", collisionOperator.Parameters[0].Type.Name);

        var genericUnion = result.OutputCompilation.GetTypeByMetadataName("GenericUnion`1")!;
        var genericOperator = Assert.Single(ConversionOperators(genericUnion));
        Assert.Equal("Value", genericOperator.Parameters[0].Type.Name);
        Assert.Equal("GenericUnion", genericOperator.ReturnType.Name);

        var aliasResult = Generate(VariantsFallbackSource());
        AssertNoGeneratorDiagnostics(aliasResult);
        AssertNoCompilationErrors(aliasResult);
        var aliasFallbackUnion = aliasResult.OutputCompilation.GetTypeByMetadataName("Variants")!;
        Assert.Single(ConversionOperators(aliasFallbackUnion));
    }

    [Fact]
    public void RecordUnion_RetainsInheritanceConversionWithoutGeneratedOperators()
    {
        var result = Generate(RecordSource());
        AssertNoGeneratorDiagnostics(result);
        AssertNoCompilationErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Outcome")!;
        Assert.Empty(ConversionOperators(union));
        var consumer = result.OutputCompilation.GetTypeByMetadataName("RecordConsumer")!;
        Assert.NotNull(consumer.GetMembers("ToBase").Single());
    }

    private static GeneratedResult Generate(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "implicit-conversions.cs");
        var compilation = CSharpCompilation.Create(
            $"ImplicitConversions_{Guid.NewGuid():N}",
            new[] { tree },
            UnityGeneratorTestHarness.GetReferences(false, false),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
                new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);
        return new GeneratedResult(
            outputCompilation,
            diagnostics);
    }

    private static IMethodSymbol[] ConversionOperators(INamedTypeSymbol type)
        => type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Conversion && method.Name == "op_Implicit")
            .ToArray();

    private static void AssertNoGeneratorDiagnostics(GeneratedResult result)
    {
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.True(diagnostics.Length == 0, string.Join("\n", diagnostics));
    }

    private static void AssertNoCompilationErrors(GeneratedResult result)
        => Assert.Empty(CompilationErrors(result.OutputCompilation));

    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> CompilationErrors(Compilation compilation)
        => compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

    private sealed record GeneratedResult(
        Compilation OutputCompilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> GeneratorDiagnostics);

    private static string BasicSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(float radius);
            [Variant] public static partial Shape Rectangle(float width, float height);
            [Variant] public static partial Shape Point();
        }
        public static class ShapeConsumer
        {
            public static Shape Circle(ShapeTypes.Variants.Circle variant) => variant;
            public static Shape Rectangle(ShapeTypes.Variants.Rectangle variant) => variant;
            public static Shape Point() => new ShapeTypes.Variants.Point();
            public static Shape? Nullable(ShapeTypes.Variants.Circle? variant) => variant;
        }
        """;

    private static string ManyVariantsSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Many
        {
            [Variant] public static partial Many A();
            [Variant] public static partial Many B(int value);
            [Variant] public static partial Many C(string value);
            [Variant] public static partial Many D(float value, int count);
            [Variant] public static partial Many E();
            [Variant] public static partial Many F(long value);
            [Variant] public static partial Many G(double value, double scale);
            [Variant] public static partial Many H(bool value);
        }
        """;

    private static string GenericAndNestedSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Result<TError, TValue>
            where TError : class
            where TValue : unmanaged
        {
            [Variant] public static partial Result<TError, TValue> Ok(TValue value);
            [Variant] public static partial Result<TError, TValue> Error(TError error);
        }
        public static partial class Outer
        {
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(float speed);
            }
        }
        public static class GenericConsumer
        {
            public static Result<string, int> Ok(ResultTypes2.Variants.Ok<string, int> value) => value;
            public static Outer.Command Run(Outer.CommandTypes.Variants.Run value) => value;
        }
        """;

    private static string ReadonlySource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public readonly partial struct ReadonlyShape
        {
            [Variant] public static partial ReadonlyShape Value(int value);
        }
        public static class ReadonlyConsumer
        {
            public static ReadonlyShape Convert(ReadonlyShapeTypes.Variants.Value value) => value;
        }
        """;

    private static string RefStructSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public ref partial struct RefShape
        {
            [Variant] public static partial RefShape Value(int value);
        }
        public static class RefConsumer
        {
            public static RefShape Convert(RefShapeTypes.Variants.Value value) => value;
        }
        """;

    private static string EscapedNullableTupleSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Escaped
        {
            [Variant] public static partial Escaped @class(
                string? @event,
                (string? label, int count) pair);
        }
        public static class EscapedConsumer
        {
            public static Escaped Convert(EscapedTypes.Variants.@class value) => value;
            public static Escaped? Lift(EscapedTypes.Variants.@class? value) => value;
        }
        """;

    private static string NonPublicSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct AccessUnion
        {
            [Variant] public static partial AccessUnion Visible(int value);
            [Variant] internal static partial AccessUnion Hidden(int value);
            [Variant] private static partial AccessUnion Secret(int value);
        }
        public static class AccessConsumer
        {
            public static AccessUnion Visible(AccessUnionTypes.Variants.Visible value) => value;
            public static AccessUnion Hidden(AccessUnionTypes.Variants.Hidden value) => value;
            public static AccessUnion Secret(AccessUnionTypes.Variants.Secret value) => value;
        }
        """;

    private static string ReverseConversionSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ForwardOnly
        {
            [Variant] public static partial ForwardOnly Value(int value);
        }
        public static class ReverseConsumer
        {
            public static ForwardOnlyTypes.Variants.Value Convert(ForwardOnly value) => value;
        }
        """;

    private static string FormerNameSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        [Serializable, DiscriminatedUnion]
        public partial struct Renamed
        {
            [Variant(FormerlySerializedAs = "Old")] public static partial Renamed Current(int value);
        }
        public static class AliasConsumer
        {
            public static Renamed Convert(RenamedTypes.Variants.Current value) => value;
            public static Renamed ConvertOld(RenamedTypes.Variants.Old value) => value;
        }
        """;

    private static string ShadowingSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct @variant
        {
            [Variant] public static partial @variant Value(int value);
        }
        [DiscriminatedUnion]
        public partial struct Collision
        {
            [Variant] public static partial Collision variant(int first, string second);
        }
        [DiscriminatedUnion]
        public partial struct GenericUnion<variant> where variant : unmanaged
        {
            [Variant] public static partial GenericUnion<variant> Value(variant value);
        }
        public static class ShadowingConsumer
        {
            public static @variant Convert(@variantTypes.Variants.Value value) => value;
            public static Collision ConvertCollision(CollisionTypes.Variants.variant value) => value;
            public static GenericUnion<int> ConvertGeneric(
                GenericUnionTypes1.Variants.Value<int> value) => value;
        }
        """;

    private static string VariantsFallbackSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Variants
        {
            [Variant] public static partial Variants Value(int value);
        }
        public static class VariantsConsumer
        {
            public static Variants Convert(VariantsTypes.Variants.Value value) => value;
        }
        """;

    private static string RecordSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public abstract partial record Outcome
        {
            public sealed partial record Success(int value) : Outcome;
            public sealed partial record Failure(string message) : Outcome;
        }
        public static class RecordConsumer
        {
            public static Outcome ToBase(Outcome.Success value) => value;
        }
        """;
}
