using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnitySerializationDiagnosticTests
{
    public static IEnumerable<object[]> InvalidDeclarationShapes => new[]
    {
        new object[] { "readonly partial struct UnityReadonly" },
        new object[] { "ref partial struct UnityRef" },
        new object[] { "partial record UnityRecord" },
        new object[] { "partial record struct UnityRecordStruct" },
    };

    public static IEnumerable<object[]> UnityFieldShapes => new[]
    {
        new object[]
        {
            "DecimalDateTimeGuid",
            "decimal decimalValue, DateTime timestamp, Guid id",
            new[] { "decimalValue", "timestamp", "id" },
        },
        new object[] { "NullableValue", "int? value", new[] { "value" } },
        new object[] { "Tuple", "(int X, int Y) value", new[] { "value" } },
        new object[] { "Dictionary", "Dictionary<int, string> value", new[] { "value" } },
        new object[] { "MultidimensionalArray", "int[,] value", new[] { "value" } },
        new object[] { "JaggedArray", "int[][] value", new[] { "value" } },
        new object[] { "NestedContainer", "List<int[]> value", new[] { "value" } },
        new object[] { "Object", "object value", new[] { "value" } },
        new object[] { "Interface", "IUnityInterfacePayload value", new[] { "value" } },
        new object[] { "AbstractClass", "AbstractUnityPayload value", new[] { "value" } },
        new object[] { "LongBackedEnum", "LongMode value", new[] { "value" } },
        new object[] { "IntPtr", "IntPtr value", new[] { "value" } },
        new object[] { "UIntPtr", "UIntPtr value", new[] { "value" } },
    };

    public static IEnumerable<object[]> SerializableHelperFieldShapes => new[]
    {
        new object[] { "Dictionary", "Dictionary<int, string>" },
        new object[] { "Nullable", "int?" },
        new object[] { "Object", "object" },
        new object[] { "IntPtr", "IntPtr" },
        new object[] { "UIntPtr", "UIntPtr" },
    };

    public static IEnumerable<object[]> ReservedMembers => new[]
    {
        new object[] { "ReservedPrefix", ReservedPrefixSource() },
        new object[] { "ReservedKind", ReservedKindSource() },
        new object[] { "ReservedSerializedPayload", ReservedSerializedPayloadSource() },
        new object[] { "ReservedCallbacks", ReservedCallbacksSource() },
    };

    public static IEnumerable<object[]> FormerlyAliasDiagnostics => new[]
    {
        new object[]
        {
            "MalformedName",
            """
                [Variant(FormerlySerializedAs = "Old-Name")]
                public static partial StabilityAliasDiagnostic First();
                """,
        },
        new object[]
        {
            "MalformedLeadingDigit",
            """
                [Variant(FormerlySerializedAs = "1Old")]
                public static partial StabilityAliasDiagnostic First();
                """,
        },
        new object[]
        {
            "EscapedAtAlias",
            """
                [Variant(FormerlySerializedAs = "@First")]
                public static partial StabilityAliasDiagnostic First();
                """,
        },
        new object[]
        {
            "CurrentVariantCollision",
            """
                [Variant(FormerlySerializedAs = "Second")]
                public static partial StabilityAliasDiagnostic First();
                [Variant] public static partial StabilityAliasDiagnostic Second();
                """,
        },
        new object[]
        {
            "AliasCollision",
            """
                [Variant(FormerlySerializedAs = "Old")]
                public static partial StabilityAliasDiagnostic First();
                [Variant(FormerlySerializedAs = "Old")]
                public static partial StabilityAliasDiagnostic Second();
                """,
        },
        new object[]
        {
            "SanitizedPayloadCollision",
            """
                [Variant(FormerlySerializedAs = "Old-Name")]
                public static partial StabilityAliasDiagnostic First();
                [Variant(FormerlySerializedAs = "Old.Name")]
                public static partial StabilityAliasDiagnostic Second();
                """,
        },
    };

    [Theory]
    [MemberData(nameof(InvalidDeclarationShapes))]
    public void SerializableInvalidDeclarationShape_ReportsDU013(string declaration)
    {
        var source = $$"""
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public {{declaration}}
            {
                [Variant] public static partial UnityDeclarationShape Value(int value);
            }
            """;

        if (declaration.StartsWith("readonly", StringComparison.Ordinal))
            source = source.Replace(
                "UnityDeclarationShape",
                "UnityReadonly",
                StringComparison.Ordinal);
        else if (declaration.StartsWith("ref", StringComparison.Ordinal))
            source = source.Replace(
                "UnityDeclarationShape",
                "UnityRef",
                StringComparison.Ordinal);
        else if (declaration.Contains("UnityRecordStruct", StringComparison.Ordinal))
            source = source.Replace(
                "UnityDeclarationShape",
                "UnityRecordStruct",
                StringComparison.Ordinal);
        else
            source = source.Replace(
                "UnityDeclarationShape",
                "UnityRecord",
                StringComparison.Ordinal);

        AssertUnityDiagnostic(source, "SPIRE_DU013");
    }

    [Theory]
    [MemberData(nameof(UnityFieldShapes))]
    public void UnityFieldShape_GeneratesTypedPayload(
        string caseName,
        string parameter,
        string[] expectedFieldNames)
    {
        var extraTypes = parameter switch
        {
            "IUnityInterfacePayload value" => "public interface IUnityInterfacePayload { }",
            "AbstractUnityPayload value" => "public abstract class AbstractUnityPayload { }",
            "LongMode value" => "public enum LongMode : long { None, Active }",
            _ => string.Empty,
        };

        var source = $$"""
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            {{extraTypes}}

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityFieldShape_{{caseName}}
            {
                [Variant] public static partial UnityFieldShape_{{caseName}} Bad({{parameter}});
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(
            result,
            "UnityFieldShape_" + caseName,
            expectedFieldNames);
    }

    [Theory]
    [MemberData(nameof(SerializableHelperFieldShapes))]
    public void SerializableHelperFieldShape_GeneratesTypedPayload(string caseName, string fieldType)
    {
        var source = $$"""
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            [Serializable]
            public sealed class UnitySerializableHelper_{{caseName}}
            {
                public {{fieldType}} serialized;
                [NonSerialized] public {{fieldType}} ignored;
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityHelperUnion_{{caseName}}
            {
                [Variant]
                public static partial UnityHelperUnion_{{caseName}} Value(
                    UnitySerializableHelper_{{caseName}} helper);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(
            result,
            "UnityHelperUnion_" + caseName,
            "helper");
    }

    [Fact]
    public void SerializableHelperNonSerializedFields_AreIgnored()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            [Serializable]
            public sealed class UnityNonSerializedHelper
            {
                [NonSerialized] public Dictionary<int, string> dictionary;
                [NonSerialized] public int? nullable;
                [NonSerialized] public object value;
                [NonSerialized] public IntPtr pointer;
                [NonSerialized] public UIntPtr unsignedPointer;
                public int supported;
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityNonSerializedHelperUnion
            {
                [Variant]
                public static partial UnityNonSerializedHelperUnion Value(
                    UnityNonSerializedHelper helper);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(result, "UnityNonSerializedHelperUnion", "helper");
    }

    [Fact]
    public void SerializableCallbackHelperContainingGeneratedUnion_IsAccepted()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityCallbackHelperChild
            {
                [Variant] public static partial UnityCallbackHelperChild Value(int value);
                [Variant] public static partial UnityCallbackHelperChild Empty();
            }

            [Serializable]
            public sealed class UnityCallbackHelperDeep
            {
                [SerializeField] private UnityCallbackHelperChild deepChild;
            }

            [Serializable]
            public class UnityCallbackHelperBase : ISerializationCallbackReceiver
            {
                [SerializeField] private UnityCallbackHelperChild baseChild;

                public void OnBeforeSerialize() { }
                public void OnAfterDeserialize() { }
            }

            [Serializable]
            public sealed class UnityCallbackHelperDerived : UnityCallbackHelperBase
            {
                [SerializeField] private UnityCallbackHelperDeep deep;
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityCallbackHelperOwner
            {
                [Variant]
                public static partial UnityCallbackHelperOwner Value(
                    UnityCallbackHelperDerived helper);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(result, "UnityCallbackHelperOwner", "helper");
    }

    [Fact]
    public void SerializableOrdinaryCallbackHelperWithoutGeneratedUnion_IsAccepted()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable]
            public sealed class UnityOrdinaryCallbackHelper : ISerializationCallbackReceiver
            {
                [SerializeField] private int value;

                public void OnBeforeSerialize() { }
                public void OnAfterDeserialize() { }
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityOrdinaryCallbackOwner
            {
                [Variant]
                public static partial UnityOrdinaryCallbackOwner Value(
                    UnityOrdinaryCallbackHelper helper);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("UnityEngine.ISerializationCallbackReceiver", result.GeneratedSource);
    }

    [Fact]
    public void GenericClosedObject_GeneratesTypedPayload()
    {
        var result = UnityGeneratorTestHarness.Run(GenericClosedArgumentSource("object"));
        AssertGeneratedPayloadFields(result, "UnityGenericFieldUnion`1", "value");
    }

    [Fact]
    public void GenericClosedDictionary_GeneratesTypedPayload()
    {
        var result = UnityGeneratorTestHarness.Run(
            GenericClosedArgumentSource("Dictionary<int, string>"));
        AssertGeneratedPayloadFields(result, "UnityGenericFieldUnion`1", "value");
    }

    [Fact]
    public void SerializableNestedGenericOwnerClosedDictionary_GeneratesTypedPayload()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            [Serializable]
            public struct UnityNestedGenericOwner<T>
            {
                public T value;
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityNestedGenericOwnerUnion
            {
                [Variant]
                public static partial UnityNestedGenericOwnerUnion Value(
                    UnityNestedGenericOwner<Dictionary<int, string>> value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(result, "UnityNestedGenericOwnerUnion", "value");
    }

    [Theory]
    [MemberData(nameof(FormerlyAliasDiagnostics))]
    public void FormerlySerializedAs_InvalidOrAmbiguousAlias_ReportsDU017(
        string _,
        string variants)
    {
        var source = $$"""
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilityAliasDiagnostic
            {
            {{variants}}
            }
            """;

        AssertAliasDiagnostic(source, "SPIRE_DU017");
    }

    [Fact]
    public void FormerlySerializedAs_UserGeneratedMemberCollision_SPIRE_DU015()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilityAliasMemberCollision
            {
                private int _kind;

                [Variant(FormerlySerializedAs = "OldValue")]
                public static partial StabilityAliasMemberCollision Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        var ids = result.GeneratorDiagnostics
            .Select(diagnostic => diagnostic.Id)
            .ToArray();
        Assert.Contains("SPIRE_DU015", ids);
        Assert.DoesNotContain("SPIRE_DU017", ids);
    }

    [Fact]
    public void UnitySerializedVariantName_GeneratesDistinctPayloadTypes()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilitySerializedVariantCollision
            {
                [Variant]
                public static partial StabilitySerializedVariantCollision Serialized(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = result.OutputCompilation.GetTypeByMetadataName(
            "StabilitySerializedVariantCollision");
        Assert.NotNull(union);
        var payloadTypes = union!.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .ToArray();
        Assert.Contains(
            payloadTypes,
            type => type.IsAbstract && type.Name == "__SpireSerializedPayloadBase");
        Assert.Contains(
            payloadTypes,
            type => !type.IsAbstract && type.Name == "__SpireSerializedPayload");
    }

    [Fact]
    public void FormerlySerializedAs_SerializedAliasGeneratesDistinctPayloadTypes()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilitySerializedAliasCollision
            {
                [Variant(FormerlySerializedAs = "Serialized")]
                public static partial StabilitySerializedAliasCollision Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = result.OutputCompilation.GetTypeByMetadataName(
            "StabilitySerializedAliasCollision");
        Assert.NotNull(union);
        var payloadTypes = union!.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .ToArray();
        Assert.Contains(
            payloadTypes,
            type => type.IsAbstract && type.Name == "__SpireSerializedPayloadBase");
        Assert.Contains(
            payloadTypes,
            type => !type.IsAbstract && type.Name == "__SpireSerializedPayload");
        Assert.Contains(
            payloadTypes,
            type => !type.IsAbstract && type.Name == "__SpireValuePayload");
    }

    [Fact]
    public void NonUnitySerializedVariantName_RemainsValid()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct StabilityNonUnitySerializedVariant
            {
                [Variant]
                public static partial StabilityNonUnitySerializedVariant Serialized();
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        Assert.DoesNotContain("SerializeReference", result.GeneratedSource);
    }

    [Fact]
    public void UnitySerializedNameSubstrings_RemainValid()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilitySerializedNameSubstringControl
            {
                [Variant]
                public static partial StabilitySerializedNameSubstringControl SerializedValue();

                [Variant(FormerlySerializedAs = "OldSerialized")]
                public static partial StabilitySerializedNameSubstringControl Value(int amount);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = result.OutputCompilation.GetTypeByMetadataName(
            "StabilitySerializedNameSubstringControl");
        Assert.NotNull(union);
        var payloadFields = union!.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(field => !field.IsStatic && field.Name == "amount")
            .ToArray();
        Assert.Equal(2, payloadFields.Length);
        Assert.All(
            payloadFields,
            field => Assert.True(HasAttribute(field, "UnityEngine.SerializeField")));
    }

    [Fact]
    public void NonSerializableConcretePayloadType_GeneratesTypedPayload()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            public sealed class UnityNonSerializableConcretePayload
            {
                public int value;
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityNonSerializableConcreteUnion
            {
                [Variant]
                public static partial UnityNonSerializableConcreteUnion Value(
                    UnityNonSerializableConcretePayload value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(
            result,
            "UnityNonSerializableConcreteUnion",
            "value");
    }

    [Fact]
    public void CustomNamespacePayloadType_GeneratesTypedPayload()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            namespace UnityCustom.Payloads
            {
                public sealed class PayloadValue
                {
                    public int value;
                }
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityCustomNamespaceUnion
            {
                [Variant]
                public static partial UnityCustomNamespaceUnion Value(
                    UnityCustom.Payloads.PayloadValue value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        AssertGeneratedPayloadFields(
            result,
            "UnityCustomNamespaceUnion",
            "value");
    }

    [Theory]
    [MemberData(nameof(ReservedMembers))]
    public void ReservedGeneratedSurface_ReportsDU015(string _, string source)
    {
        AssertUnityDiagnostic(source, "SPIRE_DU015");
    }

    [Fact]
    public void SerializableUnionWithoutUnityEngine_ReportsDU016()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityMissingEngine
            {
                [Variant] public static partial UnityMissingEngine Value(int value);
            }
            """;

        AssertUnityDiagnostic(
            source,
            "SPIRE_DU016",
            includeUnityReferences: false);
    }

    [Fact]
    public void SerializableZeroVariant_ReportsDU003AndOmitsDrawer()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityZeroVariant
            {
            }
            """;

        var result = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: true);

        Assert.Contains(
            result.GeneratorDiagnostics,
            diagnostic => diagnostic.Id == "SPIRE_DU003" &&
                diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);
    }

    private static void AssertUnityDiagnostic(
        string source,
        string expectedId,
        bool includeUnityReferences = true)
    {
        var result = UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: includeUnityReferences);

        var ids = result.GeneratorDiagnostics
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        Assert.Contains(expectedId, ids);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);
    }

    private static void AssertAliasDiagnostic(string source, string expectedId)
    {
        var result = UnityGeneratorTestHarness.Run(source);
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Id == expectedId)
            .ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(result.GeneratorDiagnostics, diagnostic =>
            Assert.Equal(expectedId, diagnostic.Id));
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.True(diagnostic.Location.IsInSource);
            var line = diagnostic.Location.SourceTree!
                .GetText()
                .Lines
                .GetLineFromPosition(diagnostic.Location.SourceSpan.Start)
                .ToString();
            Assert.Contains("FormerlySerializedAs", line);
        });
    }

    private static void AssertGeneratedPayloadFields(
        UnityGeneratorRun result,
        string metadataName,
        params string[] expectedFieldNames)
    {
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(union);

        var payloadFields = union!.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(field => !field.IsStatic)
            .ToArray();

        Assert.NotEmpty(payloadFields);
        Assert.All(payloadFields, field =>
        {
            Assert.Equal(Accessibility.Public, field.DeclaredAccessibility);
            Assert.True(
                HasAttribute(field, "UnityEngine.SerializeField"),
                $"Payload field '{field.ContainingType.Name}.{field.Name}' must carry " +
                "UnityEngine.SerializeField.");
        });

        foreach (var expectedFieldName in expectedFieldNames)
        {
            var field = payloadFields.SingleOrDefault(candidate =>
                candidate.Name == expectedFieldName);
            Assert.NotNull(
                field);
        }
    }

    private static string GenericClosedArgumentSource(string argument)
        => $$"""
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityGenericFieldUnion<T>
            {
                [Variant] public static partial UnityGenericFieldUnion<T> Some(T value);
                [Variant] public static partial UnityGenericFieldUnion<T> None();
            }

            public static class ClosedUse
            {
                public static UnityGenericFieldUnion<{{argument}}> Value
                    => UnityGenericFieldUnion<{{argument}}>.Some(default!);
            }
            """;

    private static bool HasAttribute(ISymbol symbol, string suffix)
        => symbol.GetAttributes().Any(attribute =>
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            return name is not null &&
                (name == suffix || name.EndsWith(suffix, StringComparison.Ordinal));
        });

    private static string ReservedPrefixSource()
        => """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityReservedPrefix
            {
                [Variant] public static partial UnityReservedPrefix Value(int __SpireValue);
            }
            """;

    private static string ReservedKindSource()
        => """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityReservedKind
            {
                private int _kind;
                [Variant] public static partial UnityReservedKind Value(int value);
            }
            """;

    private static string ReservedSerializedPayloadSource()
        => """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityReservedPayload
            {
                private object? _serializedPayload;
                [Variant] public static partial UnityReservedPayload Value(int value);
            }
            """;

    private static string ReservedCallbacksSource()
        => """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityReservedCallbacks
            {
                [Variant] public static partial UnityReservedCallbacks Value(int value);
                public void OnBeforeSerialize() { }
                public void OnAfterDeserialize() { }
            }
            """;
}
