using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityOverlapGeneratorTests
{
    [Fact]
    public void SerializableExplicitOverlap_AcceptsNonGenericPrimitiveAndManagedReferenceVariants()
    {
        var result = RunFixture("Serializable_ExplicitOverlap", includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityOverlapUnion");
        Assert.NotNull(union.GetMembers("Reference").OfType<IMethodSymbol>().SingleOrDefault());
        Assert.Contains("StructLayout(LayoutKind.Explicit)", result.GeneratedSource);
        Assert.Contains("FieldOffset", result.GeneratedSource);
        Assert.Contains("SerializeField", result.GeneratedSource);
        Assert.Contains("SerializeReference", result.GeneratedSource);
        Assert.Contains("Texture2D", result.GeneratedSource);
    }

    [Fact]
    public void GeneratedOverlap_UsesExplicitStorageAndAppendsSerializedReferencesWithoutOverlap()
    {
        var result = RunFixture("Serializable_ExplicitOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = EmitAndLoad(result);
        var unionType = assembly.GetType("UnityOverlapUnion");
        Assert.NotNull(unionType);
        Assert.Equal(LayoutKind.Explicit, unionType!.StructLayoutAttribute!.Value);

        var fields = InstanceFields(unionType);
        Assert.NotEmpty(fields);
        var ranges = fields
            .Select(field => (Field: field, Offset: GetFieldOffset(field), Size: FieldSize(field)))
            .OrderBy(item => item.Offset)
            .ToArray();
        for (var index = 1; index < ranges.Length; index++)
        {
            Assert.True(
                ranges[index].Offset >= ranges[index - 1].Offset + ranges[index - 1].Size,
                "Explicit Overlap fields overlap: " +
                ranges[index - 1].Field.Name + " and " + ranges[index].Field.Name);
        }

        var runtimeNames = new[] { "_kind", "_value", "_amount", "_data", "_label", "_icon" };
        var runtimeExtent = ranges
            .Where(item => runtimeNames.Contains(item.Field.Name, StringComparer.Ordinal))
            .Select(item => item.Offset + item.Size)
            .DefaultIfEmpty(0)
            .Max();
        var serializedPayload = fields.Single(field => field.Name == "_serializedPayload");
        Assert.True(
            GetFieldOffset(serializedPayload) >= runtimeExtent,
            "The serialized payload was placed inside the existing Overlap runtime extent.");

        AssertCanonicalReflectionSchema(fields);
    }

    [Fact]
    public void GeneratedOverlap_RuntimeAndPayloadFieldsAreNonSerializedExceptCanonicalSchema()
    {
        var result = RunFixture("Serializable_ExplicitOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityOverlapUnion");
        var serializedFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                (HasAttribute(field, "UnityEngine.SerializeField") ||
                    HasAttribute(field, "UnityEngine.SerializeReference")))
            .ToArray();
        Assert.Single(serializedFields);
        Assert.Contains(serializedFields, field => field.Name == "_serializedPayload");

        var payloadFields = union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .ToArray();
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_Int32);
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_Single);
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_String);
        Assert.Contains(payloadFields, field => IsNamedType(field.Type, "UnityEngine.Texture2D"));

        var runtimeFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                field.Name != "_kind" &&
                field.Name != "_serializedPayload")
            .ToArray();
        Assert.NotEmpty(runtimeFields);
        Assert.All(
            runtimeFields,
            field => Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                "Overlap runtime field was serializer-visible: " + field.Name));
        Assert.True(
            HasAttribute(
                union.GetMembers("_kind").OfType<IFieldSymbol>().Single(),
                "System.NonSerializedAttribute"));
    }

    [Fact]
    public void GeneratedOverlap_CallbacksKeepPriorSnapshotUntilSerializeAndPureFallback()
    {
        var result = RunFixture("Serializable_ExplicitOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = EmitAndLoad(result);
        var unionType = assembly.GetType("UnityOverlapUnion");
        Assert.NotNull(unionType);
        var dataType = assembly.GetType("UnityOverlapData");
        Assert.NotNull(dataType);
        var data = Activator.CreateInstance(dataType!);
        dataType!.GetField("marker")!.SetValue(data, 7);
        var factory = unionType!.GetMethod("Reference", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(factory);
        var original = factory!.Invoke(null, new object?[] { data, "before", null });
        Assert.NotNull(original);
        InvokeCallback(original!, "OnBeforeSerialize");
        var originalPayload = GetFieldValue(original!, "_serializedPayload");
        Assert.NotNull(originalPayload);

        var copy = SetGeneratedProperty(
            RuntimeHelpers.GetObjectValue(original!),
            "label",
            "after");
        Assert.Equal("before", GetPropertyValue(original!, "label"));
        Assert.Equal("after", GetPropertyValue(copy, "label"));
        Assert.Same(originalPayload, GetFieldValue(copy, "_serializedPayload"));
        InvokeCallback(copy, "OnBeforeSerialize");
        var copyPayload = GetFieldValue(copy, "_serializedPayload");
        Assert.NotNull(copyPayload);
        Assert.NotSame(originalPayload, copyPayload);
        Assert.Equal("before", GetFieldValue(originalPayload!, "label"));
        Assert.Equal("after", GetFieldValue(copyPayload!, "label"));
        Assert.Same(originalPayload, GetFieldValue(original!, "_serializedPayload"));
        Assert.Same(data, GetPropertyValue(original!, "data"));

        var fallback = InvokeFromSerialized(unionType, null);
        Assert.Equal("Number", GetPropertyValue(fallback, "kind")!.ToString());
        Assert.Equal(0, GetPropertyValue(fallback, "value"));
        Assert.Equal(0f, GetPropertyValue(fallback, "amount"));
        Assert.Null(GetPropertyValue(fallback, "data"));
        Assert.Null(GetPropertyValue(fallback, "label"));
        Assert.Null(GetPropertyValue(fallback, "icon"));
    }

    [Fact]
    public void GeneratedOverlap_ManagedReferencePayloadAndRuntimeReferenceSurviveGC()
    {
        var result = RunFixture("Serializable_ExplicitOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = EmitAndLoad(result);
        var unionType = assembly.GetType("UnityOverlapUnion")!;
        var dataType = assembly.GetType("UnityOverlapData")!;
        var data = Activator.CreateInstance(dataType);
        dataType.GetField("marker")!.SetValue(data, 99);
        var factory = unionType.GetMethod("Reference", BindingFlags.Public | BindingFlags.Static)!;
        var value = factory.Invoke(null, new object?[] { data, "gc-root", null });
        var weak = new WeakReference(data);
        data = null;
        ForceCollection();
        Assert.True(weak.IsAlive, "The Overlap runtime reference was not retained by the emitted struct.");
        Assert.Equal("gc-root", GetPropertyValue(value!, "label"));
        Assert.Equal(99, GetFieldValue(GetPropertyValue(value!, "data")!, "marker"));
        InvokeCallback(value!, "OnBeforeSerialize");
        Assert.NotNull(GetFieldValue(value!, "_serializedPayload"));
        GC.KeepAlive(value);
    }

    [Fact]
    public void SerializableExplicitOverlap_CSharp9HasNoInitDependencyOrNullableWarnings()
    {
        var result = RunFixture(
            "Serializable_ExplicitOverlap",
            languageVersion: LanguageVersion.CSharp9);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("IsExternalInit", result.GeneratedSource);
        Assert.Empty(GeneratedNullableWarnings(result));
        Assert.Contains("StructLayout(LayoutKind.Explicit)", result.GeneratedSource);
    }

    [Fact]
    public void SerializableExplicitOverlap_CompatibleNamesCompileAndKeepExplicitLayout()
    {
        var result = RunFixture(
            "Serializable_ExplicitOverlap_CompatibleNames",
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "UnityOverlapCompatibleNames");
        Assert.NotNull(union.GetMembers("buffer").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("payload").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("cacheValue").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("ownerValue").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.Contains("FieldOffset", result.GeneratedSource);
    }

    [Fact]
    public void SerializableExplicitOverlap_NestedInNonGenericContainingTypeCompiles()
    {
        var result = RunFixture(
            "Serializable_ExplicitOverlap_Nested",
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var union = FindUnion(result, "UnityOverlapContainer+Nested");
        Assert.Contains(union.GetMembers("Number").OfType<IMethodSymbol>(), method => method.IsStatic);
        Assert.Contains("CustomPropertyDrawer", result.GeneratedSource);
        Assert.Contains("FieldOffset", result.GeneratedSource);
    }

    [Fact]
    public void OverlapAndAdditive_ExposeTheSameSerializedSchema()
    {
        var result = RunFixture("Serializable_ExplicitOverlap_Parity");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var additive = FindUnion(result, "UnityOverlapParityAdditive");
        var overlap = FindUnion(result, "UnityOverlapParityExplicit");
        Assert.Equal(SerializedFieldShape(additive), SerializedFieldShape(overlap));
        Assert.Equal(PayloadFieldSignatures(additive), PayloadFieldSignatures(overlap));
        Assert.Equal(2, CountOccurrences(result.GeneratedSource, "CustomPropertyDrawer"));
    }

    [Fact]
    public void NormalNonSerializableOverlap_ControlHasNoUnitySerializationSurface()
    {
        var result = RunFixture("Normal_ExplicitOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        Assert.DoesNotContain("SerializeReference", result.GeneratedSource);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);
        Assert.Contains("StructLayout(LayoutKind.Explicit)", result.GeneratedSource);
        Assert.Contains("FieldOffset", result.GeneratedSource);
    }

    [Fact]
    public void SerializableExplicitOverlap_GenericUnionReportsDU005WithoutUnitySurface()
    {
        var result = RunFixture("Serializable_ExplicitOverlap_Generic_Diagnostic");

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU005");
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);
    }

    [Fact]
    public void SerializableExplicitOverlap_GenericContainingUnionReportsDU005WithoutUnitySurface()
    {
        var result = RunFixture("Serializable_ExplicitOverlap_GenericContaining_Diagnostic");

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU005");
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);
    }

    private static UnityGeneratorRun RunFixture(
        string fixtureName,
        bool includeEditorReferences = false,
        LanguageVersion languageVersion = LanguageVersion.Preview)
        => UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture(fixtureName),
            includeEditorReferences: includeEditorReferences,
            languageVersion: languageVersion);

    private static Assembly EmitAndLoad(UnityGeneratorRun result)
    {
        var compilation = Assert.IsType<CSharpCompilation>(result.OutputCompilation);
        return UnityGeneratorTestHarness.EmitAndLoad(
            compilation,
            "Generated Overlap assembly");
    }

    private static INamedTypeSymbol FindUnion(UnityGeneratorRun result, string metadataName)
    {
        var union = result.OutputCompilation.GetTypeByMetadataName(metadataName);
        Assert.True(union is not null, "Union '" + metadataName + "' was not generated.");
        return union!;
    }

    private static FieldInfo[] InstanceFields(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => !field.IsStatic)
            .ToArray();

    private static FieldInfo GetField(Type type, string name)
    {
        var field = type.GetField(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return field!;
    }

    private static int GetFieldOffset(FieldInfo field)
    {
        var attribute = field.GetCustomAttribute<FieldOffsetAttribute>();
        Assert.NotNull(attribute);
        return attribute!.Value;
    }

    private static int FieldSize(FieldInfo field)
    {
        if (!field.FieldType.IsValueType)
            return IntPtr.Size;
        if (field.FieldType.IsEnum)
            return Marshal.SizeOf(Enum.GetUnderlyingType(field.FieldType));
        if (field.FieldType == typeof(bool))
            return 1;
        if (field.FieldType == typeof(byte) || field.FieldType == typeof(sbyte))
            return 1;
        if (field.FieldType == typeof(short) || field.FieldType == typeof(ushort))
            return 2;
        if (field.FieldType == typeof(int) || field.FieldType == typeof(uint) ||
            field.FieldType == typeof(float))
            return 4;
        if (field.FieldType == typeof(long) || field.FieldType == typeof(ulong) ||
            field.FieldType == typeof(double))
            return 8;
        try
        {
            return Marshal.SizeOf(field.FieldType);
        }
        catch (ArgumentException)
        {
            return IntPtr.Size;
        }
    }

    private static void AssertCanonicalReflectionSchema(FieldInfo[] fields)
    {
        var serialized = fields.Where(field =>
            HasAttribute(field, "UnityEngine.SerializeField") ||
            HasAttribute(field, "UnityEngine.SerializeReference")).ToArray();
        Assert.Single(serialized);
        Assert.Contains(serialized, field => field.Name == "_serializedPayload");
        Assert.All(
            fields.Where(field => field.Name != "_serializedPayload"),
            field => Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                "Overlap runtime field was not explicitly non-serialized: " + field.Name));
    }

    private static object? GetFieldValue(object value, string name)
        => GetField(value.GetType(), name).GetValue(value);

    private static object? GetPropertyValue(object value, string name)
    {
        var property = value.GetType().GetProperty(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        return property!.GetValue(value, null);
    }

    private static void InvokeCallback(object value, string name)
    {
        var method = value.GetType().GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(value, null);
    }

    private static object InvokeFromSerialized(Type unionType, object? payload)
    {
        var methods = unionType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.Name == "__SpireFromSerialized")
            .Where(method => method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(object))
            .ToArray();
        Assert.Single(methods);
        var method = methods[0];
        var value = method.Invoke(
            method.IsStatic ? null : Activator.CreateInstance(unionType),
            new[] { payload });
        Assert.NotNull(value);
        return value!;
    }

    private static object SetGeneratedProperty(object value, string name, object newValue)
    {
        var copy = RuntimeHelpers.GetObjectValue(value);
        var property = copy.GetType().GetProperty(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.SetMethod);
        property.SetValue(copy, newValue, null);
        Assert.Equal(newValue, property.GetValue(copy, null));
        return copy;
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string[] SerializedFieldShape(INamedTypeSymbol union)
        => union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                (HasAttribute(field, "UnityEngine.SerializeField") ||
                    HasAttribute(field, "UnityEngine.SerializeReference")))
            .Select(field => field.Name + ":" + field.Type.Name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] PayloadFieldSignatures(INamedTypeSymbol union)
        => union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(field => !field.IsStatic)
            .Select(field => field.Name + ":" + field.Type.ToDisplayString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static Microsoft.CodeAnalysis.Diagnostic[] GeneratedNullableWarnings(
        UnityGeneratorRun result)
    {
        var generatedTrees = result.RunResult.GeneratedTrees.ToArray();
        return result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning &&
                diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal) &&
                diagnostic.Location.IsInSource &&
                generatedTrees.Any(tree =>
                    ReferenceEquals(tree, diagnostic.Location.SourceTree) ||
                    tree.FilePath.Length > 0 &&
                    tree.FilePath == diagnostic.Location.SourceTree?.FilePath))
            .ToArray();
    }

    private static bool IsNamedType(ITypeSymbol type, string displayName)
        => type is INamedTypeSymbol named &&
            named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::" + displayName;

    private static bool HasAttribute(ISymbol symbol, string suffix)
        => symbol.GetAttributes().Any(attribute =>
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            return name is not null &&
                (name == suffix || name.EndsWith(suffix, StringComparison.Ordinal));
        });

    private static bool HasAttribute(MemberInfo member, string fullName)
        => member.GetCustomAttributesData().Any(attribute =>
            string.Equals(attribute.AttributeType.FullName, fullName, StringComparison.Ordinal));

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
