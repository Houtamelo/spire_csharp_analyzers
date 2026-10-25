using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.UnityMetadataRegressions;

/// Unity-reference generator contracts that are independent of native asset
/// persistence and Inspector interaction.
public sealed class UnityMetadataRegressionsTests
{
    [Fact]
    public void SerializableUnion_CSharp9_HasNoGeneratedNullableWarnings()
    {
        var result = Run(
            """
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct CSharp9Shape
            {
                [Variant] public static partial CSharp9Shape Value(string? text);
            }
            """,
            LanguageVersion.CSharp9);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoGeneratedWarnings(result);

        var union = FindUnion(result, "CSharp9Shape");
        AssertCallbackImplementation(union, "OnBeforeSerialize");
        AssertCallbackImplementation(union, "OnAfterDeserialize");
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("CSharp9ShapeTypes+Variants+Value"));
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName(
            "Houtamelo.Spire.Generated.SpireSerializedPayload`1"));
    }

    [Fact]
    public void NonSerializableUnion_HasNoUnitySerializationOrDrawerSurface()
    {
        var result = Run(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct PlainShape
            {
                [Variant] public static partial PlainShape Value(int value);
            }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoGeneratedWarnings(result);

        var union = FindUnion(result, "PlainShape");
        Assert.Empty(union.GetMembers("OnBeforeSerialize"));
        Assert.Empty(union.GetMembers("OnAfterDeserialize"));
        Assert.Null(union.GetMembers("_serializedPayload").OfType<IFieldSymbol>().SingleOrDefault());
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName(
            "Houtamelo.Spire.Generated.SpireSerializedPayload`1"));
        Assert.DoesNotContain(
            result.RunResult.GeneratedTrees,
            tree => tree.GetText().ToString().Contains("UnityEditor", StringComparison.Ordinal));
    }

    [Fact]
    public void SerializableAndFactoriesOnOtherPartialDeclarations_AreDiscoveredSemantically()
    {
        var result = Run(
            """
            using System;
            using Houtamelo.Spire;
            using SerializableAlias = System.SerializableAttribute;

            public partial struct AliasShape
            {
                [Variant] public static partial AliasShape Value(int count);
            }

            [SerializableAlias]
            [DiscriminatedUnion]
            public partial struct AliasShape { }

            public partial struct QualifiedShape
            {
                [Variant] public static partial QualifiedShape Value(int count);
            }

            [global::System.Serializable]
            [DiscriminatedUnion]
            public partial struct QualifiedShape { }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var unionName in new[] { "AliasShape", "QualifiedShape" })
        {
            var union = FindUnion(result, unionName);
            Assert.NotEmpty(union.GetMembers("Value"));
            AssertCallbackImplementation(union, "OnBeforeSerialize");
            AssertCallbackImplementation(union, "OnAfterDeserialize");
            Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName(unionName + "Types+Variants+Value"));
            Assert.NotNull(union.GetMembers("_serializedPayload")
                .OfType<IFieldSymbol>().SingleOrDefault());
        }
    }

    [Fact]
    public void SerializablePayloadFields_EmitExpectedUnityMetadataMatrix()
    {
        var result = Run(
            """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            public enum MetadataMode : short
            {
                None,
                Active,
            }

            public sealed class MetadataNode
            {
                public int id;
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct MetadataUnion
            {
                [Variant]
                public static partial MetadataUnion Fields(
                    MetadataMode mode,
                    int @event,
                    List<int> plainList,
                    int[] plainArray,
                    [UnitySerializeReference] MetadataNode? managed,
                    [UnitySerializeReference] List<MetadataNode?> managedList,
                    [UnitySerializeReference] MetadataNode?[] managedArray,
                    [UnitySerializeReference] MetadataNode? @class);
            }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var payload = result.OutputCompilation.GetTypeByMetadataName("MetadataUnionTypes+Variants+Fields");
        Assert.NotNull(payload);

        foreach (var name in new[]
        {
            "mode", "event", "plainList", "plainArray",
            "managed", "managedList", "managedArray", "class",
        })
        {
            var field = FindField(payload!, name);
            if (name is not ("managed" or "managedList" or "managedArray" or "class"))
            {
                Assert.True(HasAttribute(field, "UnityEngine.SerializeField"),
                    $"Payload field '{name}' must remain a Unity serialized field.");
            }
        }

        foreach (var name in new[] { "managed", "managedList", "managedArray", "class" })
        {
            var field = FindField(payload!, name);
            Assert.True(HasAttribute(field, "UnityEngine.SerializeReference"),
                $"Managed-reference field '{name}' must carry SerializeReference.");
            Assert.False(HasAttribute(field, "UnityEngine.SerializeField"),
                $"Managed-reference field '{name}' must not carry SerializeField.");
        }

        foreach (var name in new[] { "mode", "event", "plainList", "plainArray" })
        {
            var field = FindField(payload!, name);
            Assert.False(HasAttribute(field, "UnityEngine.SerializeReference"),
                $"Unmarked field '{name}' must not carry SerializeReference.");
        }
    }

    private static UnityGeneratorRun Run(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
        => UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: true,
            languageVersion: languageVersion);

    private static INamedTypeSymbol FindUnion(UnityGeneratorRun result, string metadataName)
        => Assert.IsAssignableFrom<INamedTypeSymbol>(
            result.OutputCompilation.GetTypeByMetadataName(metadataName));

    private static IFieldSymbol FindField(INamedTypeSymbol payload, string name)
        => Assert.IsAssignableFrom<IFieldSymbol>(
            payload.GetMembers("_" + name).OfType<IFieldSymbol>().SingleOrDefault());

    private static bool HasAttribute(ISymbol? symbol, string fullName)
        => symbol?.GetAttributes().Any(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), fullName, StringComparison.Ordinal)) == true;

    private static void AssertCallbackImplementation(INamedTypeSymbol union, string callbackName)
    {
        var callbackInterface = union.AllInterfaces.Single(interfaceType =>
            interfaceType.ToDisplayString() == "UnityEngine.ISerializationCallbackReceiver");
        var interfaceMember = callbackInterface.GetMembers(callbackName)
            .OfType<IMethodSymbol>()
            .Single();
        var implementation = union.FindImplementationForInterfaceMember(interfaceMember);
        var method = Assert.IsAssignableFrom<IMethodSymbol>(implementation);
        Assert.Equal(MethodKind.ExplicitInterfaceImplementation, method.MethodKind);
    }

    private static void AssertNoGeneratedWarnings(UnityGeneratorRun result)
    {
        var generatedPaths = result.RunResult.GeneratedTrees
            .Select(tree => tree.FilePath)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToHashSet(StringComparer.Ordinal);
        var warnings = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            .Where(diagnostic => diagnostic.Location.SourceTree is { } tree &&
                generatedPaths.Contains(tree.FilePath))
            .ToArray();

        Assert.Empty(warnings);
    }
}
