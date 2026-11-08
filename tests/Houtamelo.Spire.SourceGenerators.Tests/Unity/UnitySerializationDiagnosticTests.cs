using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Current Unity payload diagnostics and Unity-owned field-shape contracts.
/// Representation-specific layout diagnostics were retired with the old API.
public sealed class UnitySerializationDiagnosticTests
{
    [Fact]
    [Trait("Environment", "Unity")]
    public void ReservedGeneratedSurface_ReportsDU015()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct ReservedSurface
            {
                public int _kind;
                [Variant] public static partial ReservedSurface Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU015");
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializablePayload_EmitsTypedUnityOwnedFields()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityOwnedFields
            {
                [Variant]
                public static partial UnityOwnedFields Some(
                    object value,
                    Dictionary<int, string> dictionary,
                    int? nullable,
                    Texture2D texture);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var payload = result.OutputCompilation.GetTypeByMetadataName("UnityOwnedFieldsTypes+Variants+Some");
        Assert.NotNull(payload);
        foreach (var name in new[] { "value", "dictionary", "nullable", "texture" })
        {
            var field = payload!.GetMembers("_" + name).OfType<IFieldSymbol>().Single();
            Assert.True(
                field.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField"));
        }
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void FormerlySerializedAs_EmitsCanonicalAndFormerPayloadTypes()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct AliasedUnion
            {
                [Variant(FormerlySerializedAs = "Old")]
                public static partial AliasedUnion Current(int value);
                [Variant] public static partial AliasedUnion Empty();
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source, includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("AliasedUnionTypes+Variants+Current"));
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("AliasedUnionTypes+Variants+Old"));
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("AliasedUnionTypes+Variants+Empty"));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void NamespaceLevelGenericUnion_EmitsClosedPayloadField()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct GenericUnion<T>
            {
                [Variant] public static partial GenericUnion<T> Some(T value);
                [Variant] public static partial GenericUnion<T> Empty();
            }

            public sealed class ClosedUses
            {
                public GenericUnion<object> value;
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var payload = AllNamedTypes(result.OutputCompilation.GlobalNamespace)
            .Single(type => type.Name == "Some" && type.TypeParameters.Length == 1);
        Assert.NotNull(payload.GetMembers("value").OfType<IPropertySymbol>().SingleOrDefault());
    }

    [Fact]
    public void SerializableUnionWithoutUnityReferences_RemainsCoreOnly()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct MissingUnity
            {
                [Variant] public static partial MissingUnity Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source, includeUnityReferences: false);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain(
            "ISerializationCallbackReceiver",
            result.GeneratedSource,
            StringComparison.Ordinal);
    }

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in AllNamedTypes(type))
                yield return nested;
        }

        foreach (var child in root.GetNamespaceMembers())
        {
            foreach (var nested in AllNamedTypes(child))
                yield return nested;
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(INamedTypeSymbol root)
    {
        foreach (var nested in root.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in AllNamedTypes(nested))
                yield return child;
        }
    }
}
