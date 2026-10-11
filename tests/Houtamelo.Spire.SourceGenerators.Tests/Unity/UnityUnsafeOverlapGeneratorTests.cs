using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityUnsafeOverlapGeneratorTests
{
    [Fact]
    public void SerializableExplicitUnsafeOverlap_AcceptsPrimitiveFloatAndManagedReferenceVariants()
    {
        var result = RunFixture("Serializable_ExplicitUnsafeOverlap", includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityUnsafeOverlap");
        Assert.NotNull(union.GetMembers("Mixed").OfType<IMethodSymbol>().SingleOrDefault());
        Assert.Contains("Unsafe.ReadUnaligned", result.GeneratedSource);
        Assert.Contains("Unsafe.WriteUnaligned", result.GeneratedSource);
        Assert.Contains("SerializeField", result.GeneratedSource);
        Assert.Contains("SerializeReference", result.GeneratedSource);
        Assert.Contains("Texture2D", result.GeneratedSource);
    }

    [Fact]
    public void SerializableExplicitUnsafeOverlap_GenericIntAndStringClosuresKeepTypedPayloadSlots()
    {
        var result = RunFixture("Serializable_ExplicitUnsafeOverlap_Generic");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityUnsafeOverlapGeneric`1");
        Assert.Contains(union.GetMembers("Some").OfType<IMethodSymbol>(), method => method.IsStatic);
        Assert.Contains(union.GetMembers("None").OfType<IMethodSymbol>(), method => method.IsStatic);
        Assert.Contains("UnityUnsafeOverlapGeneric<T>", result.GeneratedSource);

        var payloadFields = union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .ToArray();
        Assert.Contains(payloadFields, field => field.Type.TypeKind == TypeKind.TypeParameter);
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_UInt32);
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_Single);
    }

    [Fact]
    public void SerializableExplicitUnsafeOverlap_CSharp9UsesFallbackBufferAndNoInitDependency()
    {
        var result = RunFixture(
            "Serializable_ExplicitUnsafeOverlap",
            languageVersion: LanguageVersion.CSharp9);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("InlineArrayAttribute", result.GeneratedSource);
        Assert.DoesNotContain("IsExternalInit", result.GeneratedSource);
        Assert.Contains("_data", result.GeneratedSource);
        Assert.Contains("Unsafe.ReadUnaligned", result.GeneratedSource);
        Assert.Contains("Unsafe.WriteUnaligned", result.GeneratedSource);
        Assert.Empty(GeneratedNullableWarnings(result));
    }

    [Fact]
    public void SerializableExplicitUnsafeOverlap_CompatibleNamesCompileWithoutSerializedCollisions()
    {
        var result = RunFixture(
            "Serializable_ExplicitUnsafeOverlap_CompatibleNames",
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityUnsafeOverlapCompatibleNames");
        Assert.NotNull(union.GetMembers("data").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("buffer").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("element").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("payload").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.Contains("SerializeReference", result.GeneratedSource);
    }

    [Fact]
    public void GeneratedUnsafeOverlap_RuntimeStorageIsRawAndExcludedFromUnitySchema()
    {
        var result = RunFixture("Serializable_ExplicitUnsafeOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = EmitAndLoad(result);
        var unionType = assembly.GetType("UnityUnsafeOverlap");
        Assert.NotNull(unionType);

        var fields = InstanceFields(unionType!);
        var kind = fields.Single(field => field.Name == "_kind");
        var payload = fields.Single(field => field.Name == "_serializedPayload");
        Assert.False(HasAttribute(kind, "UnityEngine.SerializeField"));
        Assert.True(HasAttribute(payload, "UnityEngine.SerializeReference"));
        Assert.True(HasAttribute(kind, "System.NonSerializedAttribute"));
        Assert.False(HasAttribute(payload, "System.NonSerializedAttribute"));

        var rawBuffer = fields.SingleOrDefault(field => field.Name == "_data");
        Assert.NotNull(rawBuffer);
        Assert.True(
            rawBuffer!.FieldType.IsArray && rawBuffer.FieldType.GetElementType() == typeof(byte) ||
            rawBuffer.FieldType.IsValueType,
            "UnsafeOverlap did not retain a byte-buffer or inline-buffer runtime field.");
        Assert.True(HasAttribute(rawBuffer, "System.NonSerializedAttribute"));

        var managedSlots = fields.Where(field =>
            field.Name != "_kind" &&
            field.Name != "_serializedPayload" &&
            field.Name != "_data" &&
            field.FieldType == typeof(object)).ToArray();
        Assert.NotEmpty(managedSlots);
        Assert.All(
            fields.Where(field =>
                field.Name != "_kind" &&
                field.Name != "_serializedPayload"),
            field => Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                "UnsafeOverlap runtime field was serializer-visible: " + field.Name));
        Assert.DoesNotContain(
            fields.Where(field => field.Name == "_data"),
            field => HasAttribute(field, "UnityEngine.SerializeField") ||
                HasAttribute(field, "UnityEngine.SerializeReference"));
    }

    [Fact]
    public void GeneratedUnsafeOverlap_SerializedPayloadContainsLogicalFieldsInsteadOfRawBytes()
    {
        var result = RunFixture("Serializable_ExplicitUnsafeOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityUnsafeOverlap");
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
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_UInt32);
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_Single);
        Assert.Contains(payloadFields, field => field.Type.SpecialType == SpecialType.System_String);
        Assert.Contains(payloadFields, field => IsNamedType(field.Type, "UnityEngine.Texture2D"));
        Assert.DoesNotContain(payloadFields, field =>
            field.Type.SpecialType == SpecialType.System_Byte ||
            field.Type is IArrayTypeSymbol array &&
                array.ElementType.SpecialType == SpecialType.System_Byte);

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
                "UnsafeOverlap runtime field was not explicitly non-serialized: " + field.Name));
    }

    [Fact]
    public void GeneratedUnsafeOverlap_CallbacksKeepPriorSnapshotUntilSerializeAndPureFallback()
    {
        var result = RunFixture("Serializable_ExplicitUnsafeOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = EmitAndLoad(result);
        var unionType = assembly.GetType("UnityUnsafeOverlap");
        Assert.NotNull(unionType);
        var factory = unionType!.GetMethod(
            "Mixed",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(factory);

        var original = factory!.Invoke(
            null,
            new object?[] { (uint)11, 1.25f, "before", null });
        Assert.NotNull(original);
        InvokeCallback(original!, "OnBeforeSerialize");
        var originalPayload = GetFieldValue(original!, "_serializedPayload");
        Assert.NotNull(originalPayload);

        var copy = RuntimeHelpers.GetObjectValue(original!);
        copy = SetGeneratedProperty(copy!, "amount", 2.5f);
        Assert.Equal(1.25f, GetPropertyValue(original!, "amount"));
        Assert.Equal(2.5f, GetPropertyValue(copy, "amount"));
        Assert.Same(originalPayload, GetFieldValue(copy, "_serializedPayload"));
        InvokeCallback(copy, "OnBeforeSerialize");
        var copyPayload = GetFieldValue(copy, "_serializedPayload");
        Assert.NotNull(copyPayload);
        Assert.NotSame(originalPayload, copyPayload);
        Assert.Equal(1.25f, GetFieldValue(originalPayload!, "amount"));
        Assert.Equal(2.5f, GetFieldValue(copyPayload!, "amount"));
        Assert.Same(originalPayload, GetFieldValue(original!, "_serializedPayload"));

        var fallback = InvokeFromSerialized(unionType, null);
        Assert.Equal("Number", GetPropertyValue(fallback, "kind")!.ToString());
        Assert.Equal((uint)0, GetPropertyValue(fallback, "value"));
        Assert.Equal((uint)0, GetPropertyValue(fallback, "raw"));
        Assert.Equal(0f, GetPropertyValue(fallback, "amount"));
        Assert.Null(GetPropertyValue(fallback, "label"));
        Assert.Null(GetPropertyValue(fallback, "icon"));
    }

    [Fact]
    public void UnsafeOverlapAndAdditive_ExposeTheSameSerializedSchema()
    {
        var result = RunFixture("Serializable_ExplicitUnsafeOverlap_Parity");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var additive = FindUnion(result, "UnityUnsafeOverlapParityAdditive");
        var unsafeUnion = FindUnion(result, "UnityUnsafeOverlapParityUnsafe");
        Assert.Equal(SerializedFieldShape(additive), SerializedFieldShape(unsafeUnion));
        Assert.Equal(PayloadFieldSignatures(additive), PayloadFieldSignatures(unsafeUnion));
        Assert.Contains("CustomPropertyDrawer", result.GeneratedSource);
        Assert.Equal(
            2,
            CountOccurrencesForTest(result.GeneratedSource, "CustomPropertyDrawer"));
    }

    [Fact]
    public void NormalNonSerializableUnsafeOverlap_ControlHasNoUnitySerializationSurface()
    {
        var result = RunFixture("Normal_ExplicitUnsafeOverlap");

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        Assert.DoesNotContain("SerializeReference", result.GeneratedSource);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);
        Assert.Contains("Unsafe.ReadUnaligned", result.GeneratedSource);
        Assert.Contains("Unsafe.WriteUnaligned", result.GeneratedSource);
    }

    [Fact]
    public void SerializableExplicitUnsafeOverlap_WithoutUnsafeCompilationReportsDU009()
    {
        var result = RunUnsafeDisabled(
            UnityGeneratorTestHarness.LoadFixture("Serializable_ExplicitUnsafeOverlap"));

        Assert.Contains(
            result.GeneratorDiagnostics,
            diagnostic => diagnostic.Id == "SPIRE_DU009");
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

    private static UnityGeneratorRun RunUnsafeDisabled(string source)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var references = UnityGeneratorTestHarness.GetReferences(
                includeUnityReferences: true,
                includeEditorReferences: false)
            .ToArray();
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "unsafe-unity-case.cs");
        var sourceTrees = new List<SyntaxTree> { tree };
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(
            parseOptions,
            references);
        if (compatibilityTree is not null)
            sourceTrees.Add(compatibilityTree);
        var compilation = CSharpCompilation.Create(
            "UnityUnsafeDisabled_" + Guid.NewGuid().ToString("N"),
            sourceTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
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

    private static Assembly EmitAndLoad(UnityGeneratorRun result)
    {
        var compilation = Assert.IsType<CSharpCompilation>(result.OutputCompilation);
        return UnityGeneratorTestHarness.EmitAndLoad(
            compilation,
            "Generated UnsafeOverlap assembly");
    }

    private static INamedTypeSymbol FindUnion(
        UnityGeneratorRun result,
        string metadataName)
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

    private static object SetGeneratedProperty(
        object value,
        string propertyName,
        object newValue)
    {
        var copy = RuntimeHelpers.GetObjectValue(value);
        var property = copy.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.SetMethod);
        property.SetValue(copy, newValue, null);
        Assert.Equal(newValue, property.GetValue(copy, null));
        return copy;
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

    private static int CountOccurrencesForTest(string text, string value)
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
