using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityPayloadLifecycleTests
{
    // ── Canonical payload schema and variant inference ───────────────
    [Fact]
    public void CanonicalPayloadSchema_AllFiveLayouts()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "Unity payload schema");

        foreach (var typeName in UnityPayloadTestSupport.LayoutUnionNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var kindField = UnityPayloadTestSupport.GetField(unionType, "_kind");
            var payloadField = UnityPayloadTestSupport.GetField(unionType, "_serializedPayload");

            Assert.True(UnityPayloadTestSupport.HasAttribute(
                payloadField,
                "UnityEngine.SerializeReference"));
            Assert.False(UnityPayloadTestSupport.HasAttribute(
                kindField,
                "UnityEngine.SerializeField"));
            Assert.True(UnityPayloadTestSupport.HasAttribute(
                kindField,
                "System.NonSerializedAttribute"));

            var serializedFields = unionType
                .GetFields(UnityPayloadTestSupport.InstanceFlags)
                .Where(field => !field.IsStatic &&
                    (UnityPayloadTestSupport.HasAttribute(field, "UnityEngine.SerializeField") ||
                        UnityPayloadTestSupport.HasAttribute(field, "UnityEngine.SerializeReference")))
                .ToArray();
            Assert.Single(serializedFields);
            Assert.Equal("_serializedPayload", serializedFields[0].Name);
            Assert.False(serializedFields[0].FieldType.IsValueType);
            Assert.DoesNotContain(
                unionType.GetFields(UnityPayloadTestSupport.InstanceFlags),
                field => field.Name is "__SpireSerializationOwner" or "__SpireSerializationState");
            Assert.DoesNotContain(
                unionType.GetFields(UnityPayloadTestSupport.InstanceFlags),
                field => !field.IsStatic &&
                    field.Name.Contains("Serialization", StringComparison.Ordinal));
            Assert.DoesNotContain(
                unionType.GetNestedTypes(UnityPayloadTestSupport.InstanceFlags),
                type => type.Name.Contains("SerializationOwner", StringComparison.Ordinal));

            var payloadTypes = UnityPayloadTestSupport.ConcretePayloadTypes(unionType);
            Assert.True(
                payloadTypes.Length >= 4,
                $"{typeName} must emit a base payload and one payload per variant.");
            Assert.Contains(
                payloadTypes,
                payloadType => UnityPayloadTestSupport.DeclaredInstanceFields(payloadType).Length == 0);
            Assert.All(
                payloadTypes.SelectMany(UnityPayloadTestSupport.DeclaredInstanceFields),
                field => Assert.True(
                    UnityPayloadTestSupport.HasAttribute(field, "UnityEngine.SerializeField"),
                    $"Payload field '{field.DeclaringType!.Name}.{field.Name}' was not serialized."));
        }
    }

    [Fact]
    public void CanonicalPayloadType_InfersVariant()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "payload-driven reconstruction");

        foreach (var typeName in UnityPayloadTestSupport.LayoutUnionNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var value = UnityPayloadTestSupport.InvokeFactory(unionType, "First", 7, "payload");
            UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
            var payload = UnityPayloadTestSupport.GetPayload(value);

            Assert.NotNull(payload);
            var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, payload);

            Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
            Assert.Equal(7, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
            Assert.Equal("payload", UnityPayloadTestSupport.GetPropertyValue(restored, "label"));

            var payloadOnlyMethods = unionType
                .GetMethods(UnityPayloadTestSupport.InstanceFlags | BindingFlags.Static)
                .Where(method => method.Name == "__SpireFromSerialized")
                .ToArray();
            Assert.Single(payloadOnlyMethods);
            Assert.Equal(
                new[] { typeof(object) },
                payloadOnlyMethods[0].GetParameters().Select(parameter => parameter.ParameterType));
        }
    }

    [Fact]
    public void RuntimeKind_IsNonSerialized()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "runtime kind metadata");

        foreach (var typeName in UnityPayloadTestSupport.LayoutUnionNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var kindField = UnityPayloadTestSupport.GetField(unionType, "_kind");

            Assert.True(UnityPayloadTestSupport.HasAttribute(
                kindField,
                "System.NonSerializedAttribute"));
            Assert.False(UnityPayloadTestSupport.HasAttribute(
                kindField,
                "UnityEngine.SerializeField"));
            Assert.True(UnityPayloadTestSupport.HasAttribute(
                UnityPayloadTestSupport.GetField(unionType, "_serializedPayload"),
                "UnityEngine.SerializeReference"));
        }
    }

    [Fact]
    public void GeneratedSerializationSurface_UsesOneReferenceCacheAndHiddenImplementationMembers()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable]
            [DiscriminatedUnion(Layout.Additive)]
            public partial struct UnitySerializationFootprintProbe
            {
                [Variant]
                public static partial UnitySerializationFootprintProbe First(int amount, string label);

                [Variant]
                public static partial UnitySerializationFootprintProbe Empty();
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var assembly = UnityPayloadTestSupport.Emit(result, "serialization footprint surface");
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "UnitySerializationFootprintProbe");

        var serializedFields = unionType
            .GetFields(UnityPayloadTestSupport.InstanceFlags)
            .Where(field => !field.IsStatic &&
                (UnityPayloadTestSupport.HasAttribute(field, "UnityEngine.SerializeField") ||
                    UnityPayloadTestSupport.HasAttribute(field, "UnityEngine.SerializeReference")))
            .ToArray();

        Assert.Single(serializedFields);
        Assert.Equal("_serializedPayload", serializedFields[0].Name);
        Assert.False(serializedFields[0].FieldType.IsValueType);
        Assert.DoesNotContain(
            unionType.GetFields(UnityPayloadTestSupport.InstanceFlags),
            field => field.Name is "__SpireSerializationOwner" or "__SpireSerializationState");
        Assert.DoesNotContain(
            unionType.GetFields(UnityPayloadTestSupport.InstanceFlags),
            field => !field.IsStatic &&
                field.Name.Contains("Serialization", StringComparison.Ordinal));
        Assert.DoesNotContain(
            unionType.GetNestedTypes(UnityPayloadTestSupport.InstanceFlags),
            type => type.Name.Contains("SerializationOwner", StringComparison.Ordinal));

        var implementationMethods = unionType
            .GetMethods(UnityPayloadTestSupport.InstanceFlags | BindingFlags.Static)
            .Where(method => method.Name.StartsWith("__Spire", StringComparison.Ordinal) ||
                method.Name is "OnBeforeSerialize" or "OnAfterDeserialize")
            .Where(method => method.IsPublic || method.IsAssembly || method.IsFamily || method.IsFamilyOrAssembly)
            .ToArray();

        Assert.NotEmpty(implementationMethods);
        Assert.All(implementationMethods, method =>
        {
            var attribute = method.GetCustomAttribute<EditorBrowsableAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(EditorBrowsableState.Never, attribute!.State);
        });

        Assert.DoesNotContain("__SpireDetachSerializedCache", result.GeneratedSource);
        Assert.DoesNotContain("__SpireSetSerializedField_", result.GeneratedSource);
    }

    [Fact]
    public void FactoryConstruction_DoesNotAttachSerializedPayload()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "factory serialization allocation boundary");
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityAdditiveUnion");

        var value = UnityPayloadTestSupport.InvokeFactory(unionType, "First", 7, "factory");

        Assert.Null(UnityPayloadTestSupport.GetPayload(value));
    }

    // ── Generic, fieldless, and former-name payload reconstruction ────

    [Fact]
    public void FieldlessPayloadType_InfersVariant()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "fieldless payload reconstruction");
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityFieldlessFirstUnion");

        var value = UnityPayloadTestSupport.InvokeFactory(unionType, "First");
        UnityPayloadTestSupport.InvokeCallback(value, "OnBeforeSerialize");
        var payload = UnityPayloadTestSupport.GetPayload(value);
        var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, payload);

        Assert.NotNull(payload);
        Assert.Empty(UnityPayloadTestSupport.DeclaredInstanceFields(payload!.GetType()));
        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(restored, "label"));
    }

    [Fact]
    public void FormerNamePayloadClass_FieldParity()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "former-name payload parity");
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "StabilityMigrationUnion");
        var canonical = UnityPayloadTestSupport.FindPayloadType(
            unionType,
            new[] { "value" },
            "Renamed");
        var formerName = UnityPayloadTestSupport.FindPayloadType(
            unionType,
            new[] { "value" },
            "OldValue");

        Assert.NotEqual(canonical, formerName);
        Assert.Equal(
            UnityPayloadTestSupport.PayloadFieldSignatures(canonical),
            UnityPayloadTestSupport.PayloadFieldSignatures(formerName));
        Assert.All(
            UnityPayloadTestSupport.DeclaredInstanceFields(formerName),
            field => Assert.True(
                UnityPayloadTestSupport.HasAttribute(field, "UnityEngine.SerializeField")));
        Assert.False(formerName.IsPublic);
    }

    [Fact]
    public void GenericPayloads_ClosedIntAndString()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "closed generic payloads");
        var openType = UnityPayloadTestSupport.RequireType(assembly, "StabilityGenericUnion`1");
        var intType = openType.MakeGenericType(typeof(int));
        var stringType = openType.MakeGenericType(typeof(string));

        var intValue = UnityPayloadTestSupport.InvokeFactory(intType, "First", 17);
        var stringValue = UnityPayloadTestSupport.InvokeFactory(stringType, "First", "closed");
        UnityPayloadTestSupport.InvokeCallback(intValue, "OnBeforeSerialize");
        UnityPayloadTestSupport.InvokeCallback(stringValue, "OnBeforeSerialize");

        var intPayload = UnityPayloadTestSupport.GetPayload(intValue);
        var stringPayload = UnityPayloadTestSupport.GetPayload(stringValue);
        var intRestored = UnityPayloadTestSupport.InvokeFromSerialized(intType, intPayload);
        var stringRestored = UnityPayloadTestSupport.InvokeFromSerialized(stringType, stringPayload);

        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(intRestored));
        Assert.Equal(17, UnityPayloadTestSupport.GetPropertyValue(intRestored, "value"));
        Assert.Equal(typeof(int), UnityPayloadTestSupport.GetField(intPayload!.GetType(), "value").FieldType);
        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(stringRestored));
        Assert.Equal("closed", UnityPayloadTestSupport.GetPropertyValue(stringRestored, "value"));
        Assert.Equal(typeof(string), UnityPayloadTestSupport.GetField(stringPayload!.GetType(), "value").FieldType);
    }

    [Fact]
    public void NestedGenericPayloads_MapCurrentAndFormerName()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "nested generic payloads");
        var openType = UnityPayloadTestSupport.RequireType(assembly, "StabilityGenericUnion`1");
        var genericParent = UnityPayloadTestSupport.RequireType(assembly, "StabilityGenericParent`1")
            .MakeGenericType(typeof(int));
        var childType = openType.MakeGenericType(typeof(int));
        var formerNameType = UnityPayloadTestSupport.FindPayloadType(
            childType,
            new[] { "value" },
            "Prior");
        var formerName = Activator.CreateInstance(formerNameType, nonPublic: true)!;
        UnityPayloadTestSupport.SetField(formerName, "value", 29);
        var child = UnityPayloadTestSupport.InvokeFromSerialized(childType, formerName);
        var parent = Activator.CreateInstance(genericParent)!;
        UnityPayloadTestSupport.SetField(parent, "value", child);
        var prepared = UnitySerializationBridge.Prepare(parent);
        var restoredParent = UnitySerializationBridge.Restore(prepared);
        var restoredChild = UnityPayloadTestSupport.GetFieldValue(restoredParent!, "value")!;

        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restoredChild));
        Assert.Equal(29, UnityPayloadTestSupport.GetPropertyValue(restoredChild, "value"));
        Assert.Equal(childType, restoredChild.GetType());
    }

    // ── Pure fallback and non-Unity alias behavior ────────────────────

    [Fact]
    public void FactoryFallback_FieldedFirstDefaults()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "fielded fallback");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, "StabilityAdditiveUnion");

        var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, null);

        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(restored, "label"));
    }

    [Fact]
    public void FactoryFallback_FieldlessFirstDefaults()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "fieldless fallback");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, "StabilityFieldlessFirstUnion");

        var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, null);

        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(restored, "label"));
    }

    [Fact]
    public void MalformedPayload_ReplacesExistingRuntimeValue()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "malformed payload fallback");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, "StabilityAdditiveUnion");
        var existing = UnityPayloadTestSupport.InvokeFactory(unionType, "First", 99, "stale");

        var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, null);

        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
        Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
        Assert.Null(UnityPayloadTestSupport.GetPropertyValue(restored, "label"));
        Assert.NotEqual(
            UnityPayloadTestSupport.GetPropertyValue(existing, "amount"),
            UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
    }

    [Fact]
    public void UnknownPayloadType_DoesNotThrow()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "unknown payload fallback");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, "StabilityAdditiveUnion");
        var unknownType = UnityPayloadTestSupport.RequireType(assembly, "StabilityUnknownPayload");
        var unknown = Activator.CreateInstance(unknownType)!;

        var exception = Record.Exception(() =>
        {
            var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, unknown);
            Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
            Assert.Equal(0, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
            Assert.Null(UnityPayloadTestSupport.GetPropertyValue(restored, "label"));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void FormerNamePayload_UsesCurrentFactory()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "former-name payload reconstruction");

        foreach (var typeName in UnityPayloadTestSupport.LayoutUnionNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var formerNameType = UnityPayloadTestSupport.FindPayloadType(
                unionType,
                new[] { "amount", "label" },
                "OldFirst");
            var formerName = Activator.CreateInstance(formerNameType, nonPublic: true)!;
            UnityPayloadTestSupport.SetField(formerName, "amount", 31);
            UnityPayloadTestSupport.SetField(formerName, "label", "legacy");

            var restored = UnityPayloadTestSupport.InvokeFromSerialized(unionType, formerName);

            Assert.Equal("First", UnityPayloadTestSupport.GetKindName(restored));
            Assert.Equal(31, UnityPayloadTestSupport.GetPropertyValue(restored, "amount"));
            Assert.Equal("legacy", UnityPayloadTestSupport.GetPropertyValue(restored, "label"));
        }
    }

    [Fact]
    public void NonUnityAlias_DoesNotChangeGeneration()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "non-Unity alias behavior");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, "StabilityNonUnityAliasUnion");
        var value = UnityPayloadTestSupport.InvokeFactory(unionType, "First", 12);

        Assert.Null(UnityPayloadTestSupport.GetFieldOrNull(unionType, "_serializedPayload"));
        Assert.Null(unionType.GetMethod("__SpireFromSerialized", UnityPayloadTestSupport.InstanceFlags));
        Assert.Equal("First", UnityPayloadTestSupport.GetKindName(value));
        Assert.Equal(12, UnityPayloadTestSupport.GetPropertyValue(value, "value"));
    }

    [Fact]
    public void AliasSelfName_IsNoOp()
    {
        var result = UnityPayloadTestSupport.Run();
        var assembly = UnityPayloadTestSupport.Emit(result, "self alias behavior");
        var unionType = UnityPayloadTestSupport.RequireType(assembly, "StabilitySelfAliasUnion");
        var valuePayloadTypes = UnityPayloadTestSupport.ConcretePayloadTypes(unionType)
            .Where(type => UnityPayloadTestSupport.DeclaredInstanceFields(type)
                .Any(field => field.Name == "value"))
            .ToArray();

        Assert.Single(valuePayloadTypes);
        Assert.DoesNotContain(
            result.GeneratorDiagnostics,
            diagnostic => diagnostic.Id == "SPIRE_DU017");
    }
}

internal static class UnityPayloadTestSupport
{
    internal static readonly string[] LayoutUnionNames =
    {
        "StabilityAdditiveUnion",
        "StabilityBoxedFieldsUnion",
        "StabilityBoxedTupleUnion",
        "StabilityUnsafeOverlapUnion",
        "StabilityOverlapUnion",
    };

    internal const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal const string Source = """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityAdditiveUnion
        {
            [Variant(FormerlySerializedAs = "OldFirst")]
            public static partial StabilityAdditiveUnion First(int amount, string label);
            [Variant] public static partial StabilityAdditiveUnion Empty();
            [Variant] public static partial StabilityAdditiveUnion Nested(StabilityNestedChildUnion child);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedFields)]
        public partial struct StabilityBoxedFieldsUnion
        {
            [Variant(FormerlySerializedAs = "OldFirst")]
            public static partial StabilityBoxedFieldsUnion First(int amount, string label);
            [Variant] public static partial StabilityBoxedFieldsUnion Empty();
            [Variant] public static partial StabilityBoxedFieldsUnion Nested(StabilityNestedChildUnion child);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedTuple)]
        public partial struct StabilityBoxedTupleUnion
        {
            [Variant(FormerlySerializedAs = "OldFirst")]
            public static partial StabilityBoxedTupleUnion First(int amount, string label);
            [Variant] public static partial StabilityBoxedTupleUnion Empty();
            [Variant] public static partial StabilityBoxedTupleUnion Nested(StabilityNestedChildUnion child);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedTuple)]
        public partial struct StabilityBoxedTuplePairUnion
        {
            [Variant]
            public static partial StabilityBoxedTuplePairUnion Pair(
                StabilityNestedChildUnion child,
                int marker,
                string label);
            [Variant] public static partial StabilityBoxedTuplePairUnion Other(float value);
            [Variant] public static partial StabilityBoxedTuplePairUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedTuple)]
        public partial struct StabilityBoxedTupleGenericUnion<T>
        {
            [Variant] public static partial StabilityBoxedTupleGenericUnion<T> Value(T value);
            [Variant] public static partial StabilityBoxedTupleGenericUnion<T> Empty();
        }

        [Serializable]
        public sealed class StabilityBoxedTupleReferenceHelper
        {
            public int marker;
        }

        [Serializable]
        public sealed class StabilityEqualsAlways
        {
            public int serializedValue;

            public override bool Equals(object? obj) => obj is StabilityEqualsAlways;

            public override int GetHashCode() => 0;
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityEqualsProbeUnion
        {
            [Variant]
            public static partial StabilityEqualsProbeUnion Value(
                StabilityEqualsAlways value,
                int marker);
            [Variant] public static partial StabilityEqualsProbeUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedTuple)]
        public partial struct StabilityBoxedTupleReferenceUnion
        {
            [Variant]
            public static partial StabilityBoxedTupleReferenceUnion Reference(
                StabilityBoxedTupleReferenceHelper helper,
                int marker);
            [Variant] public static partial StabilityBoxedTupleReferenceUnion Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct StabilityUnsafeOverlapUnion
        {
            [Variant(FormerlySerializedAs = "OldFirst")]
            public static partial StabilityUnsafeOverlapUnion First(int amount, string label);
            [Variant] public static partial StabilityUnsafeOverlapUnion Empty();
            [Variant] public static partial StabilityUnsafeOverlapUnion Nested(StabilityNestedChildUnion child);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct StabilityOverlapUnion
        {
            [Variant(FormerlySerializedAs = "OldFirst")]
            public static partial StabilityOverlapUnion First(int amount, string label);
            [Variant] public static partial StabilityOverlapUnion Empty();
            [Variant] public static partial StabilityOverlapUnion Nested(StabilityNestedChildUnion child);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityFieldlessFirstUnion
        {
            [Variant] public static partial StabilityFieldlessFirstUnion First();
            [Variant] public static partial StabilityFieldlessFirstUnion Value(int amount);
            [Variant] public static partial StabilityFieldlessFirstUnion Text(string label);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityNestedChildUnion
        {
            [Variant(FormerlySerializedAs = "Prior")]
            public static partial StabilityNestedChildUnion Text(int count, string label);
            [Variant] public static partial StabilityNestedChildUnion Empty();
            [Variant] public static partial StabilityNestedChildUnion Number(float value);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityNestedParentUnion
        {
            [Variant] public static partial StabilityNestedParentUnion Child(StabilityNestedChildUnion child, int marker);
            [Variant] public static partial StabilityNestedParentUnion Empty();
            [Variant] public static partial StabilityNestedParentUnion Text(string label);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityGenericUnion<T>
        {
            [Variant(FormerlySerializedAs = "Prior")]
            public static partial StabilityGenericUnion<T> First(T value);
            [Variant] public static partial StabilityGenericUnion<T> Nested(StabilityNestedChildUnion child);
            [Variant] public static partial StabilityGenericUnion<T> Empty();
        }

        [Serializable]
        public sealed class StabilityGenericParent<T>
        {
            public StabilityGenericUnion<T> value;
            public StabilityGenericUnion<T>[] array = Array.Empty<StabilityGenericUnion<T>>();
            public List<StabilityGenericUnion<T>> list = new();
        }

        [Serializable]
        public sealed class StabilityBridgeHost
        {
            public StabilityNestedParentUnion parent;
            public StabilityNestedChildUnion[] array = Array.Empty<StabilityNestedChildUnion>();
            public List<StabilityNestedChildUnion> list = new();
        }

        [Serializable]
        public sealed class StabilityInitializerHost
        {
            public StabilityNestedChildUnion child =
                StabilityNestedChildUnion.Text(99, "initializer");
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityMigrationUnion
        {
            [Variant(FormerlySerializedAs = "OldValue")]
            public static partial StabilityMigrationUnion Renamed(int value);
            [Variant(FormerlySerializedAs = "OldEmpty")]
            public static partial StabilityMigrationUnion Empty();
            [Variant] public static partial StabilityMigrationUnion Other(string label);
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilitySelfAliasUnion
        {
            [Variant(FormerlySerializedAs = "Value")]
            public static partial StabilitySelfAliasUnion Value(int value);
            [Variant] public static partial StabilitySelfAliasUnion Empty();
        }

        [DiscriminatedUnion(Layout.Additive)]
        public partial struct StabilityNonUnityAliasUnion
        {
            [Variant(FormerlySerializedAs = "OldFirst")]
            public static partial StabilityNonUnityAliasUnion First(int value);
            [Variant] public static partial StabilityNonUnityAliasUnion Empty();
        }

        [Serializable]
        public sealed class StabilityUnknownPayload : UnitySerializationPayloadBase
        {
        }
        """;

    internal static UnityGeneratorRun Run(bool includeEditorReferences = false)
    {
        var result = UnityGeneratorTestHarness.Run(
            Source,
            includeEditorReferences: includeEditorReferences);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        return result;
    }

    internal static Assembly Emit(UnityGeneratorRun result, string description)
        => UnityGeneratorTestHarness.EmitAndLoad(
            Assert.IsType<CSharpCompilation>(result.OutputCompilation),
            description);

    internal static Type RequireType(Assembly assembly, string metadataName)
    {
        var type = assembly.GetType(metadataName);
        Assert.NotNull(type);
        return type!;
    }

    internal static FieldInfo GetField(Type type, string name)
    {
        var field = type.GetField(name, InstanceFlags);
        Assert.NotNull(field);
        return field!;
    }

    internal static FieldInfo? GetFieldOrNull(Type type, string name)
        => type.GetField(name, InstanceFlags);

    internal static object InvokeFactory(Type unionType, string name, params object?[] arguments)
    {
        var method = unionType.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var value = method!.Invoke(null, arguments);
        Assert.NotNull(value);
        return value!;
    }

    internal static object InvokeFromSerialized(Type unionType, object? payload)
    {
        var methods = unionType
            .GetMethods(InstanceFlags | BindingFlags.Static)
            .Where(method => method.Name == "__SpireFromSerialized")
            .Where(method => method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(object))
            .ToArray();
        Assert.Single(methods);
        var method = methods[0];
        var value = method.Invoke(
            method.IsStatic ? null : Activator.CreateInstance(unionType),
            new[] { payload });
        Assert.NotNull(value);
        return value!;
    }

    internal static object InvokeCallback(object value, string name)
    {
        var method = value.GetType().GetMethod(name, InstanceFlags);
        Assert.NotNull(method);
        var result = method!.Invoke(value, null);
        return result ?? value;
    }

    internal static object? GetPayload(object value)
        => GetField(value.GetType(), "_serializedPayload").GetValue(value);

    internal static string GetKindName(object value)
        => GetPropertyValue(value, "kind")!.ToString()!;

    internal static object? GetPropertyValue(object value, string name)
    {
        var property = value.GetType().GetProperty(name, InstanceFlags);
        Assert.NotNull(property);
        return property!.GetValue(value);
    }

    internal static void SetPropertyValue(object value, string name, object? propertyValue)
    {
        var property = value.GetType().GetProperty(name, InstanceFlags);
        Assert.NotNull(property);
        Assert.NotNull(property!.SetMethod);
        property.SetValue(value, propertyValue);
    }

    internal static object? GetFieldValue(object value, string name)
        => GetField(value.GetType(), name).GetValue(value);

    internal static void SetField(object value, string name, object? fieldValue)
        => GetField(value.GetType(), name).SetValue(value, fieldValue);

    internal static Type[] ConcretePayloadTypes(Type unionType)
        => unionType.GetNestedTypes(InstanceFlags)
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.IsDefined(typeof(SerializableAttribute), inherit: false))
            .ToArray();

    internal static FieldInfo[] DeclaredInstanceFields(Type type)
        => type.GetFields(InstanceFlags | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsStatic)
            .ToArray();

    internal static Type FindPayloadType(
        Type unionType,
        string[] fieldNames,
        string? nameContains = null)
    {
        var candidates = ConcretePayloadTypes(unionType)
            .Where(type => nameContains is null ||
                type.Name.Contains(nameContains, StringComparison.Ordinal))
            .Where(type => DeclaredInstanceFields(type)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(fieldNames.OrderBy(name => name, StringComparer.Ordinal)))
            .ToArray();
        Assert.Single(candidates);
        var candidate = candidates[0];
        if (!candidate.ContainsGenericParameters)
            return candidate;

        Assert.True(unionType.IsConstructedGenericType);
        return candidate.MakeGenericType(unionType.GetGenericArguments());
    }

    internal static Type FindCanonicalPayloadType(Type unionType, string[] fieldNames)
    {
        var candidates = ConcretePayloadTypes(unionType)
            .Where(type => !type.Name.Contains("Old", StringComparison.Ordinal))
            .Where(type => DeclaredInstanceFields(type)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(fieldNames.OrderBy(name => name, StringComparer.Ordinal)))
            .ToArray();
        Assert.Single(candidates);
        return candidates[0];
    }

    internal static string[] PayloadFieldSignatures(Type payloadType)
        => DeclaredInstanceFields(payloadType)
            .Select(field => field.Name + ":" + field.FieldType.AssemblyQualifiedName)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

    internal static bool HasAttribute(MemberInfo member, string fullName)
        => member.GetCustomAttributesData().Any(attribute =>
            string.Equals(attribute.AttributeType.FullName, fullName, StringComparison.Ordinal));
}
