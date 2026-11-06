using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public sealed class SerializablePayloadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnannotatedUnion_FieldedAndFieldlessPayloadsAreNotSerializable(
        bool includeUnityReferences)
    {
        var result = Generate(UnannotatedUnionSource(), includeUnityReferences);
        AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        AssertPayloadSerializable(result.OutputCompilation, "PlainUnionTypes", "Value", expected: false);
        AssertPayloadSerializable(result.OutputCompilation, "PlainUnionTypes", "Empty", expected: false);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializableUnion_FieldedAndFieldlessPayloadsAreSerializable()
    {
        var result = Generate(SerializableUnionSource(), includeUnityReferences: true);
        AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        AssertPayloadSerializable(result.OutputCompilation, "SerializableUnionTypes", "Value", expected: true);
        AssertPayloadSerializable(result.OutputCompilation, "SerializableUnionTypes", "Empty", expected: true);
        Assert.Contains("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializableUnionWithoutUnityReferences_RetainsPayloadMarker()
    {
        var result = Generate(SerializableUnionSource(), includeUnityReferences: false);
        AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU016");

        AssertPayloadSerializable(result.OutputCompilation, "SerializableUnionTypes", "Value", expected: true);
        AssertPayloadSerializable(result.OutputCompilation, "SerializableUnionTypes", "Empty", expected: true);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializableAliasAndFormerName_PreservePayloadMarker()
    {
        const string source = """
            using System;
            using SerializableAlias = System.SerializableAttribute;
            using Houtamelo.Spire;

            [SerializableAlias]
            [DiscriminatedUnion]
            public partial struct AliasedUnion
            {
                [Variant(FormerlySerializedAs = "Old")]
                public static partial AliasedUnion Current(int value);
                [Variant] public static partial AliasedUnion Empty();
            }
            """;

        var result = Generate(source, includeUnityReferences: false);
        AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var name in new[] { "Current", "Old", "Empty" })
            AssertPayloadSerializable(result.OutputCompilation, "AliasedUnionTypes", name, expected: true);
    }

    [Fact]
    public void SerializableGenericAndNestedUnions_PreservePayloadMarker()
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

            public static partial class Container
            {
                [Serializable]
                [DiscriminatedUnion]
                public partial struct Nested
                {
                    [Variant] public static partial Nested Value(int value);
                    [Variant] public static partial Nested Empty();
                }
            }
            """;

        var result = Generate(source, includeUnityReferences: false);
        AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var name in new[] { "Some", "Empty" })
            AssertPayloadSerializable(result.OutputCompilation, "GenericUnionTypes1", name, expected: true);
        foreach (var name in new[] { "Value", "Empty" })
            AssertPayloadSerializable(result.OutputCompilation, "Container+NestedTypes", name, expected: true);
    }

    [Fact]
    public void ForeignSerializableAlias_DoesNotOptInPayloadMarker()
    {
        const string source = """
            using System;
            using ForeignSerializable = Foreign.SerializableAttribute;
            using Houtamelo.Spire;

            namespace Foreign
            {
                [AttributeUsage(AttributeTargets.Struct)]
                public sealed class SerializableAttribute : Attribute { }
            }

            [ForeignSerializable]
            [DiscriminatedUnion]
            public partial struct ForeignUnion
            {
                [Variant] public static partial ForeignUnion Value(int value);
                [Variant] public static partial ForeignUnion Empty();
            }
            """;

        var result = Generate(source, includeUnityReferences: false);
        AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        AssertPayloadSerializable(result.OutputCompilation, "ForeignUnionTypes", "Value", expected: false);
        AssertPayloadSerializable(result.OutputCompilation, "ForeignUnionTypes", "Empty", expected: false);
    }

    private static UnityGeneratorRun Generate(string source, bool includeUnityReferences)
        => UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: includeUnityReferences,
            includeEditorReferences: false);

    private static void AssertNoGeneratorErrors(UnityGeneratorRun result)
    {
        var errors = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
                                 diagnostic.Id != "SPIRE_DU016")
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(
                Environment.NewLine,
                errors.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
    }

    private static void AssertPayloadSerializable(
        Compilation compilation,
        string unionName,
        string variantName,
        bool expected)
    {
        var payload = AllNamedTypes(compilation.GlobalNamespace)
            .SingleOrDefault(type =>
            {
                if (!string.Equals(type.Name.Split('`')[0], variantName, StringComparison.Ordinal) ||
                    type.ContainingType?.Name != "Variants")
                {
                    return false;
                }

                var containing = type.ContainingType.ContainingType;
                var names = new Stack<string>();
                while (containing is not null)
                {
                    names.Push(containing.Name);
                    containing = containing.ContainingType;
                }

                return string.Equals(
                    string.Join("+", names),
                    unionName,
                    StringComparison.Ordinal);
            });
        Assert.NotNull(payload);
        var actual = payload!.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.SerializableAttribute");
        Assert.Equal(expected, actual);
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

    private static string UnannotatedUnionSource() => """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct PlainUnion
        {
            [Variant] public static partial PlainUnion Value(int value);
            [Variant] public static partial PlainUnion Empty();
        }
        """;

    private static string SerializableUnionSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public partial struct SerializableUnion
        {
            [Variant] public static partial SerializableUnion Value(int value);
            [Variant] public static partial SerializableUnion Empty();
        }
        """;
}
