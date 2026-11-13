using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Integration coverage for authored positional record-struct variants.
public sealed class RecordVariantCoreTests : BehavioralTestBase
{
    [Fact]
    public void AuthoredRecordReplacesOnlyThePublicPayload()
    {
        var assembly = CompileAndLoad(BasicSource);
        var union = GetType(assembly, "Customization");
        var authoredPayload = GetType(assembly, "Customization+Slider");

        Assert.True(authoredPayload.IsValueType);
        Assert.NotNull(authoredPayload.GetConstructor(new[] { typeof(int) }));
        Assert.Null(GetTypeOrNull(assembly, "CustomizationTypes+Variants+Slider"));
        Assert.NotNull(GetType(assembly, "CustomizationTypes+Variants+Color"));
        Assert.Null(union.GetMethod("Slider", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(union.GetMethod("Color", BindingFlags.Public | BindingFlags.Static));

        var slider = Activator.CreateInstance(authoredPayload, 7)!;
        var unionValue = ConvertPayload(union, slider);
        Assert.NotNull(unionValue);
        Assert.Equal("Slider", union.GetProperty("kind")!.GetValue(unionValue)!.ToString());

        var payload = union.GetProperty("slider")!.GetValue(unionValue)!;
        Assert.Equal(authoredPayload, payload.GetType());
        Assert.Equal(7, authoredPayload.GetProperty("value")!.GetValue(payload));
    }

    [Fact]
    public void MixedFormsKeepOneDeclarationOrderedKindSequence()
    {
        var result = Generate(MixedSource);
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("MixedCustomization")!;
        var kind = union.GetTypeMembers("Kind").Single();
        Assert.Equal(0, Constant(kind, "Slider"));
        Assert.Equal(1, Constant(kind, "Color"));
        Assert.Equal(2, Constant(kind, "None"));
        Assert.Equal(3, Constant(kind, "Point"));

        var assembly = EmitCompilation(result.OutputCompilation);
        var closedUnion = GetType(assembly, "MixedCustomization");
        var color = closedUnion.GetMethod("Color")!.Invoke(null, new object[] { 12 })!;
        Assert.Equal("Color", closedUnion.GetProperty("kind")!.GetValue(color)!.ToString());
        var colorPayload = closedUnion.GetProperty("color")!.GetValue(color)!;
        Assert.Equal(12, colorPayload.GetType().GetProperty("code")!.GetValue(colorPayload));
    }

    [Fact]
    public void FieldlessRecordVariantsHaveNoGeneratedFactoryOrFields()
    {
        var assembly = CompileAndLoad(FieldlessSource);
        var union = GetType(assembly, "FieldlessCustomization");
        var none = GetType(assembly, "FieldlessCustomization+None");
        var empty = GetType(assembly, "FieldlessCustomization+Empty");

        Assert.Null(union.GetMethod("None", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(union.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static));
        Assert.DoesNotContain(none.GetConstructors(), ctor => ctor.GetParameters().Length > 0);
        Assert.DoesNotContain(empty.GetConstructors(), ctor => ctor.GetParameters().Length > 0);
        Assert.Empty(none.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(empty.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        var noneValue = Activator.CreateInstance(none)!;
        var converted = ConvertPayload(union, noneValue);
        Assert.NotNull(converted);
        Assert.Equal("None", union.GetProperty("kind")!.GetValue(converted)!.ToString());
    }

    [Fact]
    public void GenericUnionRecordPayloadUsesContainingArgumentsOnly()
    {
        var result = Generate(GenericSource);
        AssertNoErrors(result);

        var openUnion = result.OutputCompilation.GetTypeByMetadataName("GenericCustomization`1")!;
        var payload = result.OutputCompilation.GetTypeByMetadataName("GenericCustomization`1+Some")!;
        Assert.Single(openUnion.TypeParameters);
        Assert.Empty(payload.TypeParameters);
        Assert.Single(payload.ContainingType!.TypeParameters);
        Assert.Equal("T", payload.ContainingType.TypeParameters[0].Name);

        var assembly = EmitCompilation(result.OutputCompilation);
        var union = GetType(assembly, "GenericCustomization`1").MakeGenericType(typeof(string));
        var somePayload = GetType(assembly, "GenericCustomization`1+Some").MakeGenericType(typeof(string));
        var payloadValue = Activator.CreateInstance(somePayload, "value")!;
        var unionValue = ConvertPayload(union, payloadValue);
        Assert.Equal("value", union.GetProperty("some")!.GetValue(unionValue)!
            .GetType().GetProperty("value")!.GetValue(union.GetProperty("some")!.GetValue(unionValue)));
    }

    [Fact]
    public void ReadonlyRecordPayloadIsReconstructedFromStoredFields()
    {
        var assembly = CompileAndLoad(ReadonlySource);
        var union = GetType(assembly, "ReadonlyCustomization");
        var payload = GetType(assembly, "ReadonlyCustomization+Value");
        var sourcePayload = Activator.CreateInstance(payload, 19)!;
        var unionValue = ConvertPayload(union, sourcePayload);

        Assert.Equal(payload, union.GetProperty("value")!.GetValue(unionValue)!.GetType());
        Assert.Equal(19, union.GetProperty("value")!.GetValue(unionValue)!
            .GetType().GetProperty("amount")!.GetValue(union.GetProperty("value")!.GetValue(unionValue)));
    }

    [Fact]
    public void AuthoredPositionalMemberIsCopiedThenReconstructed()
    {
        var assembly = CompileAndLoad(CustomMemberSource);
        var union = GetType(assembly, "CustomMemberCustomization");
        var payload = GetType(assembly, "CustomMemberCustomization+Counter");
        var original = Activator.CreateInstance(payload, 10)!;
        Assert.Equal(11, payload.GetProperty("value")!.GetValue(original));

        var unionValue = ConvertPayload(union, original);
        var reconstructed = union.GetProperty("counter")!.GetValue(unionValue)!;
        Assert.Equal(12, payload.GetProperty("value")!.GetValue(reconstructed));
    }

    [Fact]
    public void EqualityUsesRecordPositionalFieldsInsteadOfRecordEquality()
    {
        var assembly = CompileAndLoad(EqualitySource);
        var probe = GetType(assembly, "RecordVariantEqualityProbe");

        Assert.True((bool)probe.GetMethod("SameValue")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("DifferentValue")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ObjectAndCollections")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ExtraMemberIgnored")!.Invoke(null, null)!);
    }

    [Fact]
    public void NestedRecordOnlyManagedUnionUsesManagedStorageClassification()
    {
        var result = Generate(NestedManagedSource);
        AssertNoErrors(result);
        Assert.Contains("internal struct Managed", result.GeneratedSource, StringComparison.Ordinal);

        var union = result.OutputCompilation.GetTypeByMetadataName("NestedRecordOnly");
        Assert.NotNull(union);
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("NestedRecordOnly+Node"));
    }

    [Fact]
    public void InvalidRecordVariantShapesReportGeneratorDiagnostic()
    {
        foreach (var source in new[] { InvalidClassSource, InvalidStructSource, InvalidPrivateSource, InvalidGenericSource })
        {
            GeneratorTestHelper.RunGenerator(source, out _, out var diagnostics, path: "record-variant-invalid.cs");
            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE_DU019");
        }
    }

    [Fact]
    public void PartialDeclarationsUseDeterministicSourceTreeOrder()
    {
        var result = GenerateMultiple(PartialOrderFirst, PartialOrderSecond);
        var reversed = GenerateMultiple(PartialOrderSecond, PartialOrderFirst);
        AssertNoErrors(result);
        AssertNoErrors(reversed);

        var union = result.OutputCompilation.GetTypeByMetadataName("PartialOrderUnion")!;
        var kind = union.GetTypeMembers("Kind").Single();
        Assert.Equal(0, Constant(kind, "Zed"));
        Assert.Equal(1, Constant(kind, "Alpha"));

        var reversedKind = reversed.OutputCompilation.GetTypeByMetadataName("PartialOrderUnion")!
            .GetTypeMembers("Kind").Single();
        Assert.Equal(0, Constant(reversedKind, "Alpha"));
        Assert.Equal(1, Constant(reversedKind, "Zed"));
    }

    [Fact]
    public void ReplacingMethodVariantWithRecordVariantPreservesFollowingOrdinal()
    {
        var methodResult = Generate(MethodReplacementMethodSource);
        var recordResult = Generate(MethodReplacementRecordSource);
        AssertNoErrors(methodResult);
        AssertNoErrors(recordResult);

        Assert.Equal(
            1,
            Constant(methodResult.OutputCompilation.GetTypeByMetadataName("ReplacementUnion")!.GetTypeMembers("Kind").Single(), "Second"));
        Assert.Equal(
            1,
            Constant(recordResult.OutputCompilation.GetTypeByMetadataName("ReplacementUnion")!.GetTypeMembers("Kind").Single(), "Second"));
    }

    [Fact]
    public void AcronymRecordVariantUsesExistingAccessorCasing()
    {
        var result = Generate(AcronymSource);
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("AcronymUnion")!;
        Assert.Contains(union.GetMembers("httpRequest"), member => member is IPropertySymbol);
    }

    [Fact]
    public void UnrelatedGeneratedMemberCollisionRemainsACompilerDiagnostic()
    {
        var result = Generate(RemainingCollisionSource);
        Assert.Contains(
            result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Id is "CS0102" or "CS0111");
    }

    [Fact]
    public void ReferenceFieldsAreCopiedShallowlyThroughRecordPayloads()
    {
        var assembly = CompileAndLoad(ShallowReferenceSource);
        var probe = GetType(assembly, "ShallowReferenceProbe");

        Assert.True((bool)probe.GetMethod("SameReference")!.Invoke(null, null)!);
    }

    [Fact]
    public void NestedNonGenericHostAndReadonlyUnionRemainSupported()
    {
        var nested = Generate(NestedHostSource);
        var readonlyUnion = Generate(ReadonlyUnionSource);
        AssertNoErrors(nested);
        AssertNoErrors(readonlyUnion);
        Assert.NotNull(nested.OutputCompilation.GetTypeByMetadataName("RecordHost+Nested+Node"));
        Assert.NotNull(readonlyUnion.OutputCompilation.GetTypeByMetadataName("ReadonlyRecordUnion+Value"));
    }

    [Fact]
    public void RefStructRecordVariantRetainsExistingRefUnionSurface()
    {
        var result = Generate(RefStructRecordSource);
        AssertNoErrors(result);
        var union = result.OutputCompilation.GetTypeByMetadataName("RefRecordUnion")!;
        Assert.DoesNotContain(union.AllInterfaces, interfaceType =>
            interfaceType.OriginalDefinition.ToDisplayString().StartsWith("System.IEquatable", StringComparison.Ordinal));
        Assert.DoesNotContain(union.GetMembers("Equals"), member => member is IMethodSymbol);
    }

    [Fact]
    public void RecordSyntaxRequiresCSharp10WhileMethodSyntaxRetainsCSharp9Compatibility()
    {
        var recordResult = Generate(BasicSource, LanguageVersion.CSharp10);
        var methodResult = Generate(MethodOnlySource, LanguageVersion.CSharp9);
        AssertNoErrors(recordResult);
        AssertNoErrors(methodResult);
    }

    [Fact]
    public void SecondaryConstructorsAndDeconstructOverloadsDoNotExpandRecordSchema()
    {
        var assembly = CompileAndLoad(SecondaryConstructorSource);
        var union = GetType(assembly, "SecondaryConstructorUnion");
        var payload = GetType(assembly, "SecondaryConstructorUnion+Value");
        var value = Activator.CreateInstance(
            payload,
            new object[] { 5, 99 })!;
        var unionValue = ConvertPayload(union, value);

        Assert.NotNull(payload.GetConstructor(new[] { typeof(int) }));
        Assert.NotNull(payload.GetConstructor(new[] { typeof(int), typeof(int) }));
        Assert.Null(payload.GetProperty("ignored"));
        Assert.Single(
            payload.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "amount");

        var reconstructed = union.GetProperty("value")!.GetValue(unionValue)!;
        Assert.Equal(5, payload.GetProperty("amount")!.GetValue(reconstructed));
        Assert.NotNull(payload.GetMethod("Deconstruct", new[]
        {
            typeof(int).MakeByRefType(),
            typeof(int).MakeByRefType(),
        }));

        var json = JsonSerializer.Serialize(unionValue, union);
        Assert.Contains("\"amount\"", json);
        Assert.DoesNotContain("ignored", json, StringComparison.Ordinal);
        var roundTrip = JsonSerializer.Deserialize(json, union)!;
        Assert.Equal(5, payload.GetProperty("amount")!.GetValue(union.GetProperty("value")!.GetValue(roundTrip)));
    }

    [Fact]
    public void InheritedRecordClassWithExplicitConstructorKeepsItsPropertySchema()
    {
        var assembly = CompileAndLoad(LegacyRecordClassSource);
        var valueType = GetType(assembly, "LegacyRecord+Value");
        var value = Activator.CreateInstance(valueType, new object[] { 42 })!;

        Assert.Equal(42, valueType.GetProperty("Amount")!.GetValue(value));
        Assert.Contains("42", value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void LaterPrimaryRecordPartDefinesSchemaWhenEarlierPartHasSameNamedSecondaryParameter()
    {
        var assembly = CompileAndLoad(ExactPrimaryBindingSource);
        var union = GetType(assembly, "ExactPrimaryUnion");
        var payload = GetType(assembly, "ExactPrimaryUnion+Value");
        var sourcePayload = Activator.CreateInstance(payload, new object[] { "42" })!;
        var unionValue = ConvertPayload(union, sourcePayload);
        var reconstructed = union.GetProperty("value")!.GetValue(unionValue)!;

        Assert.Equal(typeof(int), payload.GetProperty("amount")!.PropertyType);
        Assert.Equal(42, payload.GetProperty("amount")!.GetValue(reconstructed));
    }

    [Fact]
    public void OuterUnionContainingRecordOnlyManagedUnionStaysLoadable()
    {
        var generated = Generate(OuterNestedManagedSource);
        AssertNoErrors(generated);

        var explicitHelpers = AllNamedTypes(generated.OutputCompilation)
            .Where(type => type.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "StructLayoutAttribute" &&
                attribute.ConstructorArguments.Length > 0 &&
                Convert.ToInt32(attribute.ConstructorArguments[0].Value) ==
                (int)System.Runtime.InteropServices.LayoutKind.Explicit))
            .ToArray();
        Assert.DoesNotContain(explicitHelpers.SelectMany(type => type.GetMembers().OfType<IFieldSymbol>()), field =>
            field.Type.ToDisplayString().Contains("NestedOnlyUnion", StringComparison.Ordinal));

        var assembly = EmitCompilation(generated.OutputCompilation);
        var nestedUnion = GetType(assembly, "NestedOnlyUnion");
        var nested = ConvertPayload(
            nestedUnion,
            Activator.CreateInstance(GetType(assembly, "NestedOnlyUnion+Node"), "hello")!);
        var holder = Activator.CreateInstance(GetType(assembly, "NestedHolder"))!;
        GetType(assembly, "NestedHolder").GetField("Value")!.SetValue(holder, nested);

        var outer = GetType(assembly, "OuterNestedManagedUnion");
        var outerValue = outer.GetMethod("Nested")!.Invoke(null, new[] { holder })!;
        var outerPayload = outer.GetProperty("nested")!.GetValue(outerValue)!;
        var roundTripHolder = outerPayload.GetType().GetProperty("holder")!.GetValue(outerPayload)!;
        var roundTripNested = GetType(assembly, "NestedHolder").GetField("Value")!.GetValue(roundTripHolder)!;
        var roundTripNode = GetType(assembly, "NestedOnlyUnion").GetProperty("node")!.GetValue(roundTripNested)!;
        Assert.Equal("hello", roundTripNode.GetType().GetProperty("text")!.GetValue(roundTripNode));
    }

    private static int Constant(INamedTypeSymbol enumType, string name)
        => Convert.ToInt32(enumType.GetMembers(name).OfType<IFieldSymbol>().Single().ConstantValue);

    private static Type? GetTypeOrNull(Assembly assembly, string name)
        => assembly.GetType(name, throwOnError: false);

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(Compilation compilation)
    {
        static IEnumerable<INamedTypeSymbol> Descend(INamespaceOrTypeSymbol container)
        {
            foreach (var type in container.GetTypeMembers())
            {
                yield return type;
                foreach (var nested in Descend(type))
                    yield return nested;
            }

            if (container is INamespaceSymbol ns)
            {
                foreach (var child in ns.GetNamespaceMembers())
                    foreach (var nested in Descend(child))
                        yield return nested;
            }
        }

        return Descend(compilation.GlobalNamespace);
    }

    private static Assembly EmitCompilation(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private static object ConvertPayload(Type union, object payload)
    {
        var operatorMethod = union.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "op_Implicit" &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == payload.GetType());
        return operatorMethod.Invoke(null, new[] { payload })!;
    }

    private static GeneratedResult Generate(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var tree = CSharpSyntaxTree.ParseText(source,
            parseOptions,
            path: "record-variant-test.cs");
        var references = TestHarnessEnvironment.RuntimeReferences
            .Append(MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "RecordVariantCore_" + Guid.NewGuid().ToString("N"),
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        return new GeneratedResult(outputCompilation, diagnostics,
            string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(treeValue => treeValue.GetText().ToString())));
    }

    private static GeneratedResult GenerateMultiple(params string[] sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var trees = sources.Select((source, index) =>
            CSharpSyntaxTree.ParseText(source, parseOptions, path: $"record-variant-part-{index}.cs"));
        var references = TestHarnessEnvironment.RuntimeReferences
            .Append(MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "RecordVariantCoreParts_" + Guid.NewGuid().ToString("N"),
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        return new GeneratedResult(outputCompilation, diagnostics,
            string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(treeValue => treeValue.GetText().ToString())));
    }

    private sealed record GeneratedResult(
        Compilation OutputCompilation,
        System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        string GeneratedSource);

    private static void AssertNoErrors(GeneratedResult generated)
    {
        var errors = generated.Diagnostics.Concat(generated.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);
    }

    private const string BasicSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant] public record struct Slider(int value);
            [Variant] public static partial Customization Color(int code);
        }
        """;

    private const string MixedSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct MixedCustomization
        {
            [Variant] public record struct Slider(int value);
            [Variant] public static partial MixedCustomization Color(int code);
            [Variant] public record struct None;
            [Variant] public static partial MixedCustomization Point();
        }
        """;

    private const string FieldlessSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct FieldlessCustomization
        {
            [Variant] public record struct None;
            [Variant] public record struct Empty();
        }
        """;

    private const string GenericSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct GenericCustomization<T>
        {
            [Variant] public record struct Some(T value);
            [Variant] public static partial GenericCustomization<T> None();
        }
        """;

    private const string ReadonlySource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ReadonlyCustomization
        {
            [Variant] public readonly record struct Value(int amount);
        }
        """;

    private const string CustomMemberSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct CustomMemberCustomization
        {
            [Variant] public record struct Counter(int value)
            {
                public int value { get; init; } = value + 1;
                public int Doubled => value * 2;
            }
        }
        """;

    private const string EqualitySource = """
        using System;
        using System.Collections.Generic;
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct RecordEqualityCustomization
        {
            [Variant] public record struct Value(int value)
            {
                public int Extra { get; set; }
                public override int GetHashCode() => 123;
            }
        }
        public static class RecordVariantEqualityProbe
        {
            public static bool SameValue()
            {
                RecordEqualityCustomization first = new RecordEqualityCustomization.Value(4);
                RecordEqualityCustomization second = new RecordEqualityCustomization.Value(4);
                return first == second && first.Equals(second) && first.GetHashCode() == second.GetHashCode();
            }
            public static bool DifferentValue()
            {
                RecordEqualityCustomization first = new RecordEqualityCustomization.Value(4);
                RecordEqualityCustomization second = new RecordEqualityCustomization.Value(5);
                return first != second && !first.Equals(second);
            }
            public static bool ObjectAndCollections()
            {
                RecordEqualityCustomization value = new RecordEqualityCustomization.Value(4);
                object boxed = value;
                var set = new HashSet<RecordEqualityCustomization> { value };
                return boxed.Equals(value) && set.Contains(value);
            }
            public static bool ExtraMemberIgnored()
            {
                var firstPayload = new RecordEqualityCustomization.Value(4) { Extra = 1 };
                var secondPayload = new RecordEqualityCustomization.Value(4) { Extra = 2 };
                RecordEqualityCustomization first = firstPayload;
                RecordEqualityCustomization second = secondPayload;
                return first == second;
            }
        }
        """;

    private const string NestedManagedSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NestedRecordOnly
        {
            [Variant] public record struct Node(string text);
            [Variant] public record struct Empty;
        }
        """;

    private const string PartialOrderFirst = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct PartialOrderUnion
        {
            [Variant] public record struct Zed(int value);
        }
        """;

    private const string PartialOrderSecond = """
        using Houtamelo.Spire;
        public partial struct PartialOrderUnion
        {
            [Variant] public record struct Alpha(int value);
        }
        """;

    private const string MethodReplacementMethodSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ReplacementUnion
        {
            [Variant] public static partial ReplacementUnion First(int value);
            [Variant] public static partial ReplacementUnion Second();
        }
        """;

    private const string MethodReplacementRecordSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ReplacementUnion
        {
            [Variant] public record struct First(int value);
            [Variant] public static partial ReplacementUnion Second();
        }
        """;

    private const string AcronymSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct AcronymUnion
        {
            [Variant] public record struct HTTPRequest(int value);
        }
        """;

    private const string RemainingCollisionSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct RemainingCollisionUnion
        {
            [Variant] public record struct Value(int value);
            public int value;
        }
        """;

    private const string ShallowReferenceSource = """
        using System;
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ShallowReferenceUnion
        {
            [Variant] public record struct Reference(int[] values);
        }
        public static class ShallowReferenceProbe
        {
            public static bool SameReference()
            {
                var values = new[] { 1 };
                ShallowReferenceUnion union = new ShallowReferenceUnion.Reference(values);
                var payload = union.reference;
                values[0] = 2;
                return ReferenceEquals(values, payload.values) && payload.values[0] == 2;
            }
        }
        """;

    private const string NestedHostSource = """
        using Houtamelo.Spire;
        public static partial class RecordHost
        {
            [DiscriminatedUnion]
            public partial struct Nested
            {
                [Variant] public record struct Node(int value);
            }
        }
        """;

    private const string ReadonlyUnionSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public readonly partial struct ReadonlyRecordUnion
        {
            [Variant] public record struct Value(int value);
        }
        """;

    private const string RefStructRecordSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public ref partial struct RefRecordUnion
        {
            [Variant] public record struct Value(int value);
        }
        """;

    private const string MethodOnlySource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct MethodOnlyUnion
        {
            [Variant] public static partial MethodOnlyUnion Value(int value);
        }
        """;

    private const string SecondaryConstructorSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial struct SecondaryConstructorUnion
        {
            [Variant]
            public record struct Value(int amount)
            {
                public Value(int amount, int ignored) : this(amount) { }
                public void Deconstruct(out int amount, out int ignored)
                {
                    amount = this.amount;
                    ignored = 99;
                }
            }
        }
        """;

    private const string LegacyRecordClassSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public abstract partial record LegacyRecord
        {
            public partial record Value : LegacyRecord
            {
                public Value(int Amount) { this.Amount = Amount; }
                public int Amount { get; }
            }
        }
        """;

    private const string ExactPrimaryBindingSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ExactPrimaryUnion
        {
            [Variant]
            public partial record struct Value
            {
                public Value(string amount) : this(int.Parse(amount)) { }
            }
        }

        public partial struct ExactPrimaryUnion
        {
            public partial record struct Value(int amount);
        }
        """;

    private const string OuterNestedManagedSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NestedOnlyUnion
        {
            [Variant] public record struct Node(string text);
        }

        public struct NestedHolder
        {
            public NestedOnlyUnion Value;
        }

        [DiscriminatedUnion]
        public partial struct OuterNestedManagedUnion
        {
            [Variant] public static partial OuterNestedManagedUnion Nested(NestedHolder holder);
            [Variant] public static partial OuterNestedManagedUnion Number(int value);
        }
        """;

    private const string InvalidClassSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct InvalidRecordUnion
        {
            [Variant] public record Item(int value);
        }
        """;

    private const string InvalidStructSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct InvalidStructUnion
        {
            [Variant] public struct Item { public int value; }
        }
        """;

    private const string InvalidPrivateSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct InvalidPrivateUnion
        {
            [Variant] private record struct Item(int value);
        }
        """;

    private const string InvalidGenericSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct InvalidGenericUnion
        {
            [Variant] public record struct Item<T>(T value);
        }
        """;
}
