using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityNativeValueStabilityTests
{
    private static readonly string[] NativeUnionTypeNames =
    {
        "UnityNativeAdditive",
        "UnityNativeBoxedFields",
        "UnityNativeBoxedTuple",
        "UnityNativeUnsafeOverlap",
        "UnityNativeOverlap",
    };

    private static readonly string[] NativeValueFieldNames =
    {
        "vector2",
        "vector3",
        "quaternion",
        "color",
        "rect",
        "bounds",
    };

    [Fact]
    public void EqualUnityValueStructs_ReuseExistingPayloadAcrossLayouts()
    {
        var assembly = EmitNativeValueSource();

        foreach (var typeName in NativeUnionTypeNames)
        {
            var unionType = UnityPayloadTestSupport.RequireType(assembly, typeName);
            var prior = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Values",
                CreateNativeArguments(unionType));
            UnityPayloadTestSupport.InvokeCallback(prior, "OnBeforeSerialize");
            var priorPayload = UnityPayloadTestSupport.GetPayload(prior);

            var current = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Values",
                CreateNativeArguments(unionType));
            var prepared = UnitySerializationBridge.Prepare(current, prior)!;

            Assert.NotNull(priorPayload);
            Assert.Same(
                priorPayload,
                UnityPayloadTestSupport.GetPayload(prepared));
        }
    }

    [Fact]
    public void ChangedUnityValueStructs_ReplaceExistingPayload()
    {
        var assembly = EmitNativeValueSource();
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "UnityNativeAdditive");

        foreach (var fieldName in NativeValueFieldNames)
        {
            var prior = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Values",
                CreateNativeArguments(unionType));
            UnityPayloadTestSupport.InvokeCallback(prior, "OnBeforeSerialize");
            var priorPayload = UnityPayloadTestSupport.GetPayload(prior);

            var currentArguments = CreateNativeArguments(unionType, fieldName);
            var current = UnityPayloadTestSupport.InvokeFactory(
                unionType,
                "Values",
                currentArguments);
            var prepared = UnitySerializationBridge.Prepare(current, prior)!;
            var preparedPayload = UnityPayloadTestSupport.GetPayload(prepared);

            Assert.NotNull(priorPayload);
            Assert.NotNull(preparedPayload);
            Assert.NotSame(priorPayload, preparedPayload);
            Assert.Equal(
                currentArguments[FieldIndex(unionType, fieldName)],
                UnityPayloadTestSupport.GetFieldValue(preparedPayload!, fieldName));
        }
    }

    [Fact]
    public void UnityObjectReferences_UseIdentityForPayloadReuseAndReplacement()
    {
        var assembly = EmitNativeValueSource();
        var unionType = UnityPayloadTestSupport.RequireType(
            assembly,
            "UnityNativeAdditive");
        var priorReference = CreateUnityObject(unionType);
        var priorArguments = CreateNativeArguments(unionType);
        priorArguments[FieldIndex(unionType, "reference")] = priorReference;
        var prior = UnityPayloadTestSupport.InvokeFactory(
            unionType,
            "Values",
            priorArguments);
        UnityPayloadTestSupport.InvokeCallback(prior, "OnBeforeSerialize");
        var priorPayload = UnityPayloadTestSupport.GetPayload(prior);

        var sameReferenceArguments = CreateNativeArguments(unionType);
        sameReferenceArguments[FieldIndex(unionType, "reference")] = priorReference;
        var sameReference = UnityPayloadTestSupport.InvokeFactory(
            unionType,
            "Values",
            sameReferenceArguments);
        var reused = UnitySerializationBridge.Prepare(sameReference, prior)!;

        Assert.NotNull(priorPayload);
        Assert.Same(
            priorPayload,
            UnityPayloadTestSupport.GetPayload(reused));

        var changedReferenceArguments = CreateNativeArguments(unionType);
        changedReferenceArguments[FieldIndex(unionType, "reference")] =
            CreateUnityObject(unionType);
        var changedReference = UnityPayloadTestSupport.InvokeFactory(
            unionType,
            "Values",
            changedReferenceArguments);
        var replaced = UnitySerializationBridge.Prepare(changedReference, prior)!;

        Assert.NotSame(
            priorPayload,
            UnityPayloadTestSupport.GetPayload(replaced));
    }

    [Fact]
    public void EditorDrawer_CSharp9GeneratedTreesHaveNoNullabilityOrObsoleteWarnings()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Drawer_Basic"),
            includeEditorReferences: true,
            languageVersion: LanguageVersion.CSharp9);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var generatedTrees = result.RunResult.GeneratedTrees.ToArray();
        var warnings = result.OutputCompilation
            .GetDiagnostics()
            .Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning &&
                diagnostic.Id is "CS8604" or "CS0618" &&
                diagnostic.Location.IsInSource &&
                generatedTrees.Any(tree =>
                    ReferenceEquals(tree, diagnostic.Location.SourceTree) ||
                    (tree.FilePath.Length > 0 &&
                        tree.FilePath == diagnostic.Location.SourceTree?.FilePath)))
            .ToArray();

        Assert.True(
            warnings.Length == 0,
            "Generated C# 9 editor source emitted CS8604/CS0618 warnings:\n" +
            string.Join(
                "\n",
                warnings.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
    }

    private static Assembly EmitNativeValueSource()
    {
        var result = UnityGeneratorTestHarness.Run(Source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        return UnityGeneratorTestHarness.EmitAndLoad(
            Assert.IsType<CSharpCompilation>(result.OutputCompilation),
            "Unity native value stability probe");
    }

    private static object?[] CreateNativeArguments(
        Type unionType,
        string? changedField = null)
    {
        var parameters = unionType
            .GetMethod("Values", BindingFlags.Public | BindingFlags.Static)!
            .GetParameters();
        var vector3Type = parameters
            .Single(parameter => parameter.Name == "vector3")
            .ParameterType;

        var arguments = parameters
            .Select(parameter => parameter.Name switch
            {
                "vector2" => CreateValue(
                    parameter.ParameterType,
                    changedField == "vector2"
                        ? new object[] { 7f, 8f }
                        : new object[] { 1f, 2f }),
                "vector3" => CreateValue(
                    parameter.ParameterType,
                    changedField == "vector3"
                        ? new object[] { 9f, 10f, 11f }
                        : new object[] { 3f, 4f, 5f }),
                "quaternion" => CreateValue(
                    parameter.ParameterType,
                    changedField == "quaternion"
                        ? new object[] { 1f, 1.5f, 2f, 2.5f }
                        : new object[] { 0f, 0.5f, 1f, 1.5f }),
                "color" => CreateValue(
                    parameter.ParameterType,
                    changedField == "color"
                        ? new object[] { 0.6f, 0.7f, 0.8f, 0.9f }
                        : new object[] { 0.1f, 0.2f, 0.3f, 0.4f }),
                "rect" => CreateValue(
                    parameter.ParameterType,
                    changedField == "rect"
                        ? new object[] { 11f, 22f, 33f, 44f }
                        : new object[] { 10f, 20f, 30f, 40f }),
                "bounds" => CreateBounds(
                    parameter.ParameterType,
                    vector3Type,
                    changedField == "bounds"),
                "reference" => null,
                _ => throw new InvalidOperationException(
                    "Unexpected native stability parameter: " + parameter.Name),
            })
            .ToArray();

        return arguments;
    }

    private static object CreateBounds(
        Type boundsType,
        Type vector3Type,
        bool changed)
    {
        var center = CreateValue(
            vector3Type,
            changed
                ? new object[] { 10f, 20f, 30f }
                : new object[] { 1f, 2f, 3f });
        var size = CreateValue(
            vector3Type,
            changed
                ? new object[] { 40f, 50f, 60f }
                : new object[] { 4f, 5f, 6f });
        return CreateValue(boundsType, center, size);
    }

    private static object CreateValue(Type type, params object[] arguments)
        => Activator.CreateInstance(type, arguments)!;

    private static object CreateUnityObject(Type unionType)
    {
        var objectType = unionType
            .GetMethod("Values", BindingFlags.Public | BindingFlags.Static)!
            .GetParameters()
            .Single(parameter => parameter.Name == "reference")
            .ParameterType;
        return RuntimeHelpers.GetUninitializedObject(objectType);
    }

    private static int FieldIndex(Type unionType, string fieldName)
        => Array.FindIndex(
            unionType
                .GetMethod("Values", BindingFlags.Public | BindingFlags.Static)!
                .GetParameters(),
            parameter => parameter.Name == fieldName);

    private const string Source = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable]
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct UnityNativeAdditive
        {
            [Variant]
            public static partial UnityNativeAdditive Values(
                Vector2 vector2,
                Vector3 vector3,
                Quaternion quaternion,
                Color color,
                Rect rect,
                Bounds bounds,
                UnityEngine.Object? reference);
            [Variant] public static partial UnityNativeAdditive Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedFields)]
        public partial struct UnityNativeBoxedFields
        {
            [Variant]
            public static partial UnityNativeBoxedFields Values(
                Vector2 vector2,
                Vector3 vector3,
                Quaternion quaternion,
                Color color,
                Rect rect,
                Bounds bounds,
                UnityEngine.Object? reference);
            [Variant] public static partial UnityNativeBoxedFields Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.BoxedTuple)]
        public partial struct UnityNativeBoxedTuple
        {
            [Variant]
            public static partial UnityNativeBoxedTuple Values(
                Vector2 vector2,
                Vector3 vector3,
                Quaternion quaternion,
                Color color,
                Rect rect,
                Bounds bounds,
                UnityEngine.Object? reference);
            [Variant] public static partial UnityNativeBoxedTuple Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct UnityNativeUnsafeOverlap
        {
            [Variant]
            public static partial UnityNativeUnsafeOverlap Values(
                Vector2 vector2,
                Vector3 vector3,
                Quaternion quaternion,
                Color color,
                Rect rect,
                Bounds bounds,
                UnityEngine.Object? reference);
            [Variant] public static partial UnityNativeUnsafeOverlap Empty();
        }

        [Serializable]
        [DiscriminatedUnion(Layout.Overlap)]
        public partial struct UnityNativeOverlap
        {
            [Variant]
            public static partial UnityNativeOverlap Values(
                Vector2 vector2,
                Vector3 vector3,
                Quaternion quaternion,
                Color color,
                Rect rect,
                Bounds bounds,
                UnityEngine.Object? reference);
            [Variant] public static partial UnityNativeOverlap Empty();
        }
        """;
}
