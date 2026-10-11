using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityManagedReferenceGeneratorTests
{
    private static readonly (string Fixture, string MetadataName)[] AllLayouts =
    {
        ("ManagedReference_AllLayouts", "ManagedReferenceAdditiveUnion"),
        ("ManagedReference_AllLayouts", "ManagedReferenceBoxedFieldsUnion"),
        ("ManagedReference_AllLayouts", "ManagedReferenceBoxedTupleUnion"),
        ("ManagedReference_AllLayouts", "ManagedReferenceUnsafeOverlapUnion"),
        ("ManagedReference_AllLayouts", "ManagedReferenceOverlapUnion"),
    };

    [Fact]
    public void ManagedReference_AllLayouts_FieldMetadata()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_AllLayouts"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var (_, metadataName) in AllLayouts)
        {
            var union = FindUnion(result, metadataName);
            AssertPayloadField(union, "__SpireReferencePayload", "value", managed: true);
            AssertPayloadField(union, "__SpireOldReferencePayload", "value", managed: true);
            AssertPayloadField(union, "__SpireInlinePayload", "value", managed: false);
            AssertPayloadField(union, "__SpirePairPayload", "first", managed: true);
            AssertPayloadField(union, "__SpirePairPayload", "second", managed: false);
            AssertFieldlessPayload(union, "__SpireEmptyPayload");
        }
    }

    [Fact]
    public void ManagedReference_CanonicalAndFormerNameParity()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_AllLayouts"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var (_, metadataName) in AllLayouts)
        {
            var union = FindUnion(result, metadataName);
            var current = FindPayloadType(union, "__SpireReferencePayload");
            var formerNamePayload = FindPayloadType(union, "__SpireOldReferencePayload");
            Assert.Equal(
                PayloadFieldSignatures(current),
                PayloadFieldSignatures(formerNamePayload));
            Assert.Equal(
                PayloadFieldMetadata(current),
                PayloadFieldMetadata(formerNamePayload));
        }
    }

    [Fact]
    public void ManagedReference_MultipleParameters_MixedAttributes()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_AllLayouts"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "ManagedReferenceAdditiveUnion");

        AssertPayloadField(union, "__SpirePairPayload", "first", managed: true);
        AssertPayloadField(union, "__SpirePairPayload", "second", managed: false);
    }

    [Fact]
    public void ManagedReference_Collections_KeepDeclaredContainerTypes()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_Collections"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "ManagedReferenceCollectionsUnion");
        var array = AssertPayloadField(
            union,
            "__SpireValuesPayload",
            "arrayValues",
            managed: true);
        var list = AssertPayloadField(
            union,
            "__SpireValuesPayload",
            "listValues",
            managed: true);
        Assert.Equal(TypeKind.Array, array.Type.TypeKind);
        Assert.Equal("List", Assert.IsType<INamedTypeSymbol>(list.Type, exactMatch: false).Name);
    }

    [Fact]
    public void ManagedReference_QualifiedAndAliasedSpireAttribute()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_Bindings"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "ManagedReferenceBindingUnion");
        AssertPayloadField(union, "__SpireValuePayload", "qualified", managed: true);
        AssertPayloadField(union, "__SpireValuePayload", "aliased", managed: true);
        AssertPayloadField(union, "__SpireValuePayload", "foreign", managed: false);
        AssertPayloadField(union, "__SpireValuePayload", "inline", managed: false);
    }

    [Fact]
    public void ManagedReference_ExactMetadataNameFromForeignAssemblyDoesNotOptIn()
    {
        var foreignReference = BuildForeignMarkerReference();
        var result = RunWithAdditionalReference(
            """
                extern alias ForeignSpire;
                using System;
                using Houtamelo.Spire;
                using ForeignMarker = ForeignSpire::Houtamelo.Spire.UnitySerializeReferenceAttribute;
                using UnityEngine;

                [Serializable]
                public sealed class MetadataSpoofNode
                {
                    public int amount;
                }

                [Serializable]
                [DiscriminatedUnion(Layout.Additive)]
                public partial struct MetadataSpoofUnion
                {
                    [Variant]
                    public static partial MetadataSpoofUnion Actual(
                        [UnitySerializeReference] MetadataSpoofNode value);

                    [Variant]
                    public static partial MetadataSpoofUnion Foreign(
                        [ForeignMarker] MetadataSpoofNode value);
                }
                """,
            foreignReference);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "MetadataSpoofUnion");
        AssertPayloadField(union, "__SpireActualPayload", "value", managed: true);
        AssertPayloadField(union, "__SpireForeignPayload", "value", managed: false);
    }

    [Fact]
    public void ManagedReference_NestedGenericContainingType_FieldMetadata()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_NestedGenericContaining"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var container = result.OutputCompilation.GetTypeByMetadataName(
            "ManagedReferenceGenericContainer`1");
        Assert.NotNull(container);
        var union = container!.GetTypeMembers("NestedUnion").Single();
        AssertPayloadField(union, "__SpireValuePayload", "value", managed: true);
        AssertFieldlessPayload(union, "__SpireEmptyPayload");
    }

    [Fact]
    public void ManagedReference_GenericBaseInterfaceAndUnitySensitiveShapes()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_GenericShapes"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "ManagedReferenceGenericUnion`1");

        AssertPayloadField(union, "__SpireGenericPayload", "genericValue", managed: true);
        AssertPayloadField(union, "__SpireBasePayload", "baseValue", managed: true);
        AssertPayloadField(union, "__SpireInterfacePayload", "interfaceValue", managed: true);
        AssertPayloadField(union, "__SpireObjectPayload", "objectValue", managed: true);
        AssertPayloadField(union, "__SpireUnityObjectPayload", "unityObjectValue", managed: true);
        AssertPayloadField(union, "__SpireDictionaryPayload", "dictionaryValue", managed: true);
        AssertPayloadField(union, "__SpireMultiPayload", "multiValue", managed: true);
        AssertPayloadField(union, "__SpireArrayPayload", "arrayValues", managed: true);
        AssertPayloadField(union, "__SpireListPayload", "listValues", managed: true);
    }

    [Fact]
    public void ManagedReference_CSharp9_And_EditorCompile()
    {
        var source = UnityGeneratorTestHarness.LoadFixture("ManagedReference_AllLayouts");
        var runtime = UnityGeneratorTestHarness.Run(
            source,
            languageVersion: LanguageVersion.CSharp9);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(runtime);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(runtime);

        var editor = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: true,
            languageVersion: LanguageVersion.CSharp9);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(editor);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(editor);
        Assert.Contains("CustomPropertyDrawer", editor.GeneratedSource);
    }

    [Fact]
    public void ManagedReference_NonUnityUnionUnchanged()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("ManagedReference_NonUnity"),
            includeUnityReferences: false);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        Assert.DoesNotContain("SerializeReference", result.GeneratedSource);
        Assert.DoesNotContain("UnityEngine", result.GeneratedSource);
    }

    [Fact]
    public void ManagedReference_AttributeTargetsParameter()
    {
        const string source = """
            using Houtamelo.Spire;

            public sealed class ManagedReferenceAttributeTargetProbe
            {
                [UnitySerializeReference]
                private int field;

                public static void Valid([UnitySerializeReference] object value) { }
            }
            """;

        var result = UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: false);
        Assert.Contains(
            result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == "CS0592" &&
                diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static INamedTypeSymbol FindUnion(
        UnityGeneratorRun result,
        string metadataName)
    {
        var union = result.OutputCompilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(union);
        return union!;
    }

    private static INamedTypeSymbol FindPayloadType(
        INamedTypeSymbol union,
        string payloadTypeName)
    {
        var payload = union.GetTypeMembers()
            .SingleOrDefault(type =>
                HasAttribute(type, "System.SerializableAttribute") &&
                type.Name == payloadTypeName);
        Assert.NotNull(payload);
        return payload!;
    }

    private static IFieldSymbol AssertPayloadField(
        INamedTypeSymbol union,
        string payloadTypeName,
        string fieldName,
        bool managed)
    {
        var payload = FindPayloadType(union, payloadTypeName);
        var field = payload.GetMembers(fieldName)
            .OfType<IFieldSymbol>()
            .SingleOrDefault();
        Assert.NotNull(field);
        Assert.Equal(Accessibility.Public, field!.DeclaredAccessibility);
        Assert.True(HasAttribute(field, "UnityEngine.SerializeField"));
        Assert.Equal(managed, HasAttribute(field, "UnityEngine.SerializeReference"));
        return field;
    }

    private static void AssertFieldlessPayload(
        INamedTypeSymbol union,
        string payloadTypeName)
    {
        var payload = FindPayloadType(union, payloadTypeName);
        Assert.Empty(payload.GetMembers().OfType<IFieldSymbol>());
    }

    private static string[] PayloadFieldSignatures(INamedTypeSymbol payload)
        => payload.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic)
            .Select(field => field.Name + ":" + field.Type.ToDisplayString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] PayloadFieldMetadata(INamedTypeSymbol payload)
        => payload.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic)
            .Select(field => field.Name + ":" +
                HasAttribute(field, "UnityEngine.SerializeField") + ":" +
                HasAttribute(field, "UnityEngine.SerializeReference"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static bool HasAttribute(ISymbol symbol, string suffix)
        => symbol.GetAttributes().Any(attribute =>
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            return name is not null &&
                (name == suffix || name.EndsWith(suffix, StringComparison.Ordinal));
        });

    private static MetadataReference BuildForeignMarkerReference()
    {
        const string source = """
            using System;

            namespace Houtamelo.Spire
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class UnitySerializeReferenceAttribute : Attribute
                {
                }
            }
            """;
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "ForeignSpireMarker_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions, path: "foreign-marker.cs") },
            TestHarnessEnvironment.FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(
            emit.Success,
            "The foreign marker reference assembly could not be emitted:\n" +
            string.Join("\n", emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray())
            .WithAliases(ImmutableArray.Create("ForeignSpire"));
    }

    private static UnityGeneratorRun RunWithAdditionalReference(
        string source,
        MetadataReference additionalReference)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "unity-metadata-spoof-case.cs");
        var references = UnityGeneratorTestHarness.GetReferences(
                includeUnityReferences: true,
                includeEditorReferences: false)
            .Append(additionalReference)
            .ToArray();
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(
            parseOptions,
            references);
        var trees = compatibilityTree is null
            ? new[] { syntaxTree }
            : new[] { syntaxTree, compatibilityTree };
        var compilation = CSharpCompilation.Create(
            "UnityManagedReferenceBinding_" + Guid.NewGuid().ToString("N"),
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
        return new UnityGeneratorRun(
            outputCompilation,
            generatorDiagnostics,
            driver.GetRunResult());
    }
}
