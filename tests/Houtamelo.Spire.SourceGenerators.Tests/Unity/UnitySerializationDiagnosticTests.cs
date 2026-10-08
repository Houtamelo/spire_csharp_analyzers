using System;
using System.Collections.Generic;
using System.Linq;
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

    public static IEnumerable<object[]> UnsupportedFieldShapes => new[]
    {
        new object[]
        {
            "DecimalDateTimeGuid",
            "decimal decimalValue, DateTime timestamp, Guid id",
        },
        new object[] { "NullableValue", "int? value" },
        new object[] { "Tuple", "(int X, int Y) value" },
        new object[] { "Dictionary", "Dictionary<int, string> value" },
        new object[] { "MultidimensionalArray", "int[,] value" },
        new object[] { "JaggedArray", "int[][] value" },
        new object[] { "NestedContainer", "List<int[]> value" },
        new object[] { "Object", "object value" },
        new object[] { "Interface", "IUnsupported value" },
        new object[] { "AbstractClass", "AbstractUnsupported value" },
        new object[] { "LongBackedEnum", "LongMode value" },
        new object[] { "IntPtr", "IntPtr value" },
        new object[] { "UIntPtr", "UIntPtr value" },
    };

    public static IEnumerable<object[]> SerializableHelperUnsupportedFieldShapes => new[]
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
    [MemberData(nameof(UnsupportedFieldShapes))]
    public void UnsupportedUnityFieldShape_ReportsDU014(string caseName, string parameter)
    {
        var extraTypes = parameter switch
        {
            "IUnsupported value" => "interface IUnsupported { }",
            "AbstractUnsupported value" => "abstract class AbstractUnsupported { }",
            "LongMode value" => "enum LongMode : long { None, Active }",
            _ => string.Empty,
        };

        var source = $$"""
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            {{extraTypes}}

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityUnsupported_{{caseName}}
            {
                [Variant] public static partial UnityUnsupported_{{caseName}} Bad({{parameter}});
            }
            """;

        AssertUnityDiagnostic(source, "SPIRE_DU014");
    }

    [Theory]
    [MemberData(nameof(SerializableHelperUnsupportedFieldShapes))]
    public void SerializableHelperUnsupportedField_ReportsDU014(string caseName, string fieldType)
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

        AssertUnityDiagnostic(source, "SPIRE_DU014");
    }

    [Fact]
    public void SerializableHelperNonSerializedUnsupportedFields_AreIgnored()
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
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("UnityNonSerializedHelperUnion", result.GeneratedSource);
    }

    [Fact]
    public void SerializableCallbackHelperContainingGeneratedUnion_ReportsDU014()
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
        Assert.Contains(
            result.GeneratorDiagnostics,
            diagnostic => diagnostic.Id == "SPIRE_DU014");
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
    public void GenericClosedObject_ReportsDU014()
    {
        AssertUnityDiagnostic(GenericClosedArgumentSource("object"), "SPIRE_DU014");
    }

    [Fact]
    public void GenericClosedDictionary_ReportsDU014()
    {
        AssertUnityDiagnostic(
            GenericClosedArgumentSource("Dictionary<int, string>"),
            "SPIRE_DU014");
    }

    [Fact]
    public void SerializableNestedGenericOwnerClosedDictionary_ReportsDU014()
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

        AssertUnityDiagnostic(source, "SPIRE_DU014");
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

    private static string GenericClosedArgumentSource(string argument)
        => $$"""
            using System;
            using System.Collections.Generic;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityGenericUnsupported<T>
            {
                [Variant] public static partial UnityGenericUnsupported<T> Some(T value);
                [Variant] public static partial UnityGenericUnsupported<T> None();
            }

            public static class ClosedUse
            {
                public static UnityGenericUnsupported<{{argument}}> Value
                    => UnityGenericUnsupported<{{argument}}>.Some(default!);
            }
            """;

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
