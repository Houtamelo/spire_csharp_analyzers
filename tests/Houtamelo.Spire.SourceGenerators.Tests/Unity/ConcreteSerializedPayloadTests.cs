using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Behavioral contract for concrete Unity managed-reference payload wrappers.
[Trait("Environment", "Unity")]
public sealed class ConcreteSerializedPayloadTests
{
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const string SharedPayloadMetadataName =
        "Houtamelo.Spire.Generated.SpireSerializedPayload`1";

    // ── Symbols and runtime type identity ───────────────────────────────────

    [Fact]
    public void CanonicalSymbols_KeepSharedBaseAndConcreteWrapperSurface()
    {
        var result = Run(ShapeSource());
        AssertValid(result);

        var shared = RequireType(result, SharedPayloadMetadataName);
        Assert.False(shared.IsSealed);
        Assert.Single(shared.TypeParameters);
        Assert.True(HasAttribute(shared, "System.SerializableAttribute"));
        Assert.True(HasAttribute(shared, "UnityEngine.Scripting.PreserveAttribute"));

        var valueField = shared.GetMembers("value").OfType<IFieldSymbol>().Single();
        Assert.True(SymbolEqualityComparer.Default.Equals(valueField.Type, shared.TypeParameters[0]));
        Assert.True(HasAttribute(valueField, "UnityEngine.SerializeField"));

        foreach (var variant in new[] { "Move", "Speak", "Empty" })
        {
            var wrapper = RequireType(result, WrapperMetadata("ConcreteWrapperShape", variant));
            var payload = RequireType(
                result,
                $"ConcreteWrapperShapeTypes+Variants+{variant}");

            AssertWrapperSurface(wrapper, shared, payload);
            Assert.Empty(DeclaredInstanceFields(wrapper));
        }
    }

    [Fact]
    public void Preparation_CreatesReusesAndReplacesCanonicalConcreteCache()
    {
        var result = Run(ShapeSource());
        AssertValid(result);
        var assembly = Load(result, "concrete wrapper preparation");
        var shape = assembly.GetType("ConcreteWrapperShape", throwOnError: true)!;

        var freshMove = InvokeFactory(shape, "Move", 7, "dash");
        Assert.Null(GetSerializedPayload(freshMove));
        var prepared = InvokeInstance(freshMove, "__SpirePrepareForSerialization");
        var payload = AssertWrapper(
            prepared,
            "ConcreteWrapperShapeTypes+SerializedPayloads+MoveSerializedPayload");
        Assert.Equal(7, ReadPayloadField(prepared, "destination"));
        Assert.Equal("dash", ReadPayloadField(prepared, "label"));

        var preparedAgain = InvokeInstance(prepared, "__SpirePrepareForSerialization");
        Assert.Same(payload, GetSerializedPayload(preparedAgain));

        var changed = InvokeFactory(shape, "Move", 8, "dash");
        SetSerializedPayload(changed, payload);
        var replaced = InvokeInstance(changed, "__SpirePrepareForSerialization");
        var changedPayload = AssertWrapper(
            replaced,
            "ConcreteWrapperShapeTypes+SerializedPayloads+MoveSerializedPayload");
        Assert.NotSame(payload, changedPayload);
        Assert.Equal(8, ReadPayloadField(replaced, "destination"));

        var freshEmpty = InvokeFactory(shape, "Empty");
        Assert.Null(GetSerializedPayload(freshEmpty));
        var preparedEmpty = InvokeInstance(freshEmpty, "__SpirePrepareForSerialization");
        AssertWrapper(
            preparedEmpty,
            "ConcreteWrapperShapeTypes+SerializedPayloads+EmptySerializedPayload");
        var emptyWrapper = GetSerializedPayload(preparedEmpty);
        Assert.NotNull(emptyWrapper);
        Assert.Empty(DeclaredRuntimeInstanceFields(emptyWrapper!.GetType()));
    }

    [Fact]
    public void Compatibility_RestoresAndUpgradesLegacyGenericWrapper()
    {
        var result = Run(ShapeSource());
        AssertValid(result);
        var assembly = Load(result, "legacy generic wrapper compatibility");
        var shape = assembly.GetType("ConcreteWrapperShape", throwOnError: true)!;
        var payloadType = assembly.GetType(
            "ConcreteWrapperShapeTypes+Variants+Move",
            throwOnError: true)!;
        var payload = CreatePayload(payloadType, 7, "dash");
        var legacy = WrapPayload(assembly, payloadType, payload);

        var state = InvokeFactory(shape, "Move", 7, "dash");
        SetSerializedPayload(state, legacy);
        Assert.False(IsStateCurrent(state, legacy));

        var restored = InvokeRestore(shape, legacy);
        Assert.Equal("Move", ReadKind(restored));
        Assert.Equal(7, ReadPayloadField(restored, "destination"));
        Assert.Equal("dash", ReadPayloadField(restored, "label"));

        var prepared = InvokeInstance(state, "__SpirePrepareForSerialization");
        var concrete = AssertWrapper(
            prepared,
            "ConcreteWrapperShapeTypes+SerializedPayloads+MoveSerializedPayload");
        Assert.NotSame(legacy, concrete);
        Assert.True(IsStateCurrent(prepared, concrete));
        Assert.Equal(7, ReadPayloadField(prepared, "destination"));
        Assert.Equal("dash", ReadPayloadField(prepared, "label"));
    }

    [Fact]
    public void FormerAliases_RestoreGenericAndConcreteThenCanonicalize()
    {
        var result = Run(AliasSource());
        AssertValid(result);

        var shared = RequireType(result, SharedPayloadMetadataName);
        var currentPayload = RequireType(
            result,
            "ConcreteWrapperAliasTypes+Variants+Current");
        var oldPayload = RequireType(
            result,
            "ConcreteWrapperAliasTypes+Variants+Old");
        var emptyPayload = RequireType(
            result,
            "ConcreteWrapperAliasTypes+Variants+Empty");
        var priorEmptyPayload = RequireType(
            result,
            "ConcreteWrapperAliasTypes+Variants+PriorEmpty");

        foreach (var (variant, payload) in new[]
        {
            ("Current", currentPayload),
            ("Old", oldPayload),
            ("Empty", emptyPayload),
            ("PriorEmpty", priorEmptyPayload),
        })
        {
            var wrapper = RequireType(result, WrapperMetadata("ConcreteWrapperAlias", variant));
            AssertWrapperSurface(wrapper, shared, payload);
        }

        var currentFields = InstanceFieldShapes(currentPayload);
        var oldFields = InstanceFieldShapes(oldPayload);
        Assert.Equal(currentFields, oldFields);
        Assert.Empty(DeclaredInstanceFields(emptyPayload));
        Assert.Empty(DeclaredInstanceFields(priorEmptyPayload));

        var assembly = Load(result, "former concrete wrapper compatibility");
        var alias = assembly.GetType("ConcreteWrapperAlias", throwOnError: true)!;
        var oldPayloadType = assembly.GetType(
            "ConcreteWrapperAliasTypes+Variants+Old",
            throwOnError: true)!;
        var oldValue = CreatePayload(oldPayloadType, 31);

        var formerGeneric = WrapPayload(assembly, oldPayloadType, oldValue);
        var restoredGeneric = InvokeRestore(alias, formerGeneric);
        Assert.Equal("Current", ReadKind(restoredGeneric));
        Assert.Equal(31, ReadPayloadField(restoredGeneric, "value"));

        var formerConcrete = CreateConcreteWrapper(
            assembly,
            "ConcreteWrapperAliasTypes+SerializedPayloads+OldSerializedPayload",
            oldValue);
        var restoredConcrete = InvokeRestore(alias, formerConcrete);
        Assert.Equal("Current", ReadKind(restoredConcrete));
        Assert.Equal(31, ReadPayloadField(restoredConcrete, "value"));

        var current = InvokeFactory(alias, "Current", 31);
        SetSerializedPayload(current, formerConcrete);
        var canonical = InvokeInstance(current, "__SpirePrepareForSerialization");
        var canonicalPayload = AssertWrapper(
            canonical,
            "ConcreteWrapperAliasTypes+SerializedPayloads+CurrentSerializedPayload");
        Assert.Equal(31, ReadPayloadField(canonical, "value"));
        var canonicalAgain = InvokeInstance(canonical, "__SpirePrepareForSerialization");
        Assert.Same(canonicalPayload, GetSerializedPayload(canonicalAgain));

        var priorEmpty = CreateConcreteWrapper(
            assembly,
            "ConcreteWrapperAliasTypes+SerializedPayloads+PriorEmptySerializedPayload",
            CreatePayload(
                assembly.GetType(
                    "ConcreteWrapperAliasTypes+Variants+PriorEmpty",
                    throwOnError: true)!));
        var restoredEmpty = InvokeRestore(alias, priorEmpty);
        Assert.Equal("Empty", ReadKind(restoredEmpty));

        var empty = InvokeFactory(alias, "Empty");
        SetSerializedPayload(empty, priorEmpty);
        AssertWrapper(
            InvokeInstance(empty, "__SpirePrepareForSerialization"),
            "ConcreteWrapperAliasTypes+SerializedPayloads+EmptySerializedPayload");
    }

    [Fact]
    public void NestedAndCollectionPreparation_UpgradesLegacyChildCaches()
    {
        var result = Run(NestedSource());
        AssertValid(result);
        var assembly = Load(result, "nested concrete wrapper preparation");
        var childType = assembly.GetType("ConcreteWrapperChild", throwOnError: true)!;
        var childPayloadType = assembly.GetType(
            "ConcreteWrapperChildTypes+Variants+Value",
            throwOnError: true)!;
        var nestedType = assembly.GetType("ConcreteWrapperNested", throwOnError: true)!;

        var legacyChild = InvokeFactory(childType, "Value", 11);
        SetSerializedPayload(
            legacyChild,
            WrapPayload(
                assembly,
                childPayloadType,
                CreatePayload(childPayloadType, 11)));

        var sibling = InvokeInstance(InvokeFactory(childType, "Value", 23), "__SpirePrepareForSerialization");
        var siblingPayload = GetSerializedPayload(sibling);
        Assert.NotNull(siblingPayload);

        var nestedChild = InvokeFactory(nestedType, "Child", legacyChild);
        var preparedChild = InvokeInstance(nestedChild, "__SpirePrepareForSerialization");
        var childFromPayload = ReadPayloadField(preparedChild, "child")!;
        AssertChildHasCanonicalPayload(childFromPayload);
        Assert.Equal(11, ReadPayloadField(childFromPayload, "value"));

        var childArray = Array.CreateInstance(childType, 2);
        childArray.SetValue(legacyChild, 0);
        childArray.SetValue(sibling, 1);
        var nestedArray = InvokeFactory(nestedType, "Array", childArray);
        var preparedArray = InvokeInstance(nestedArray, "__SpirePrepareForSerialization");
        var arrayFromPayload = (Array)ReadPayloadField(preparedArray, "children")!;
        AssertChildHasCanonicalPayload(arrayFromPayload.GetValue(0)!);
        Assert.Equal(11, ReadPayloadField(arrayFromPayload.GetValue(0)!, "value"));
        Assert.Same(siblingPayload, GetSerializedPayload(arrayFromPayload.GetValue(1)!));

        var childList = (IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(childType))!;
        childList.Add(legacyChild);
        childList.Add(sibling);
        var nestedList = InvokeFactory(nestedType, "List", childList);
        var preparedList = InvokeInstance(nestedList, "__SpirePrepareForSerialization");
        var listFromPayload = (IList)ReadPayloadField(preparedList, "children")!;
        AssertChildHasCanonicalPayload(listFromPayload[0]!);
        Assert.Equal(11, ReadPayloadField(listFromPayload[0]!, "value"));
        Assert.Same(siblingPayload, GetSerializedPayload(listFromPayload[1]!));
    }

    // ── Scope, identifier, and generic-shape probes ─────────────────────────

    [Fact]
    public void ScopeSymbols_StayUniqueAcrossNamespacesNestingPrivateAndEscapedNames()
    {
        var result = Run(ScopeSource());
        AssertValid(result);

        var leftMove = RequireType(
            result,
            "Deep.Layer.ConcreteWrapperLeftTypes+SerializedPayloads+MoveSerializedPayload");
        var rightMove = RequireType(
            result,
            "Deep.Layer.ConcreteWrapperRightTypes+SerializedPayloads+MoveSerializedPayload");
        Assert.False(SymbolEqualityComparer.Default.Equals(leftMove, rightMove));
        Assert.NotNull(
            result.OutputCompilation.GetTypeByMetadataName(
                "Deep.Layer.ConcreteWrapperOwner+NestedTypes+SerializedPayloads+RunSerializedPayload"));
        Assert.NotNull(
            result.OutputCompilation.GetTypeByMetadataName(
                "Deep.Layer.ConcreteWrapperOwner+HiddenTypes+SerializedPayloads+ValueSerializedPayload"));

        var escapedWrappers = AllNamedTypes(result.OutputCompilation)
            .Where(type => type.ContainingType?.Name == "SerializedPayloads" &&
                type.ContainingType.ContainingType?.Name == "ConcreteWrapperEscapedNamesTypes")
            .ToArray();
        Assert.Contains(escapedWrappers, type =>
            type.Name.StartsWith("event", StringComparison.Ordinal));
        Assert.All(escapedWrappers, type => Assert.DoesNotContain("@", type.Name));
    }

    [Fact]
    public void GenericSymbols_PreserveArityConstraintsAndContainingScope()
    {
        var result = Run(GenericSource());
        // Generic containing unions retain the existing diagnostic policy; the
        // generated source must still compile and preserve the type shape.
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var generic = RequireType(result, "ConcreteWrapperGeneric`1");
        Assert.Single(generic.TypeParameters);

        var wrappers = AllNamedTypes(result.OutputCompilation)
            .Where(type => type.ContainingType?.Name == "SerializedPayloads" &&
                type.Name.EndsWith("SerializedPayload", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(wrappers, type => type.Name == "ValueSerializedPayload");
        Assert.Contains(wrappers, type => type.Name == "OldValueSerializedPayload");
        Assert.Contains(wrappers, type => type.Name == "EmptySerializedPayload");
        Assert.All(wrappers, wrapper =>
        {
            Assert.NotEmpty(AllTypeParameters(wrapper));
            var payload = Assert.IsAssignableFrom<INamedTypeSymbol>(
                wrapper.BaseType!.TypeArguments.Single());
            Assert.Equal("Variants", payload.ContainingType?.Name);
        });

        var containingGenericWrappers = wrappers
            .Where(type => type.ToDisplayString().Contains(
                "ConcreteWrapperGenericOwner", StringComparison.Ordinal))
            .ToArray();
        Assert.Collection(containingGenericWrappers, _ => { }, _ => { });
        Assert.All(
            containingGenericWrappers,
            wrapper => Assert.Contains(
                AllTypeParameters(wrapper),
                parameter => parameter.HasReferenceTypeConstraint));

        var closedUses = RequireType(result, "ConcreteWrapperGenericUses");
        Assert.NotEmpty(closedUses.GetMembers("IntValue"));
        Assert.NotEmpty(closedUses.GetMembers("StringValue"));
    }

    [Fact]
    public void Controls_DoNotEmitSerializedPayloadWrappersForNonSerializableOrUnrelatedTypes()
    {
        var result = Run(ControlSource());
        AssertValid(result);

        Assert.NotNull(
            result.OutputCompilation.GetTypeByMetadataName(
                "ConcreteWrapperSerializableTypes+SerializedPayloads+ValueSerializedPayload"));
        Assert.NotNull(
            result.OutputCompilation.GetTypeByMetadataName("ConcreteWrapperPlain"));
        Assert.Null(
            result.OutputCompilation.GetTypeByMetadataName(
                "ConcreteWrapperPlainTypes+SerializedPayloads+ValueSerializedPayload"));
        Assert.DoesNotContain(
            AllNamedTypes(result.OutputCompilation),
            type => type.Name.EndsWith("SerializedPayload", StringComparison.Ordinal) &&
                type.ToDisplayString().Contains("UnrelatedSerializable", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratedSource_IsVersionIndependentAndInspectorUsesInheritedValueFallback()
    {
        var csharp9 = Run(ShapeSource(), languageVersion: LanguageVersion.CSharp9);
        var preview = Run(ShapeSource(), languageVersion: LanguageVersion.Preview);
        AssertValid(csharp9);
        AssertValid(preview);

        var expectedNames = new[]
        {
            "EmptySerializedPayload",
            "MoveSerializedPayload",
            "SpeakSerializedPayload",
        };
        Assert.Equal(expectedNames, WrapperNames(csharp9));
        Assert.Equal(expectedNames, WrapperNames(preview));

        foreach (var result in new[] { csharp9, preview })
        {
            var wrapperSource = string.Join(
                "\n",
                result.RunResult.GeneratedTrees
                    .Select(tree => tree.GetText().ToString())
                    .Where(text => text.Contains("MoveSerializedPayload", StringComparison.Ordinal)));
            Assert.NotEmpty(wrapperSource);
            Assert.DoesNotContain("UNITY_2022", wrapperSource, StringComparison.Ordinal);
            Assert.DoesNotContain("UNITY_2023", wrapperSource, StringComparison.Ordinal);
            Assert.DoesNotContain("UNITY_6000", wrapperSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Application.unityVersion", wrapperSource, StringComparison.Ordinal);
        }

        var editor = Run(ShapeSource(), includeEditorReferences: true);
        AssertValid(editor);
        var editorSource = editor.GeneratedSource;
        Assert.Contains("\"value\"", editorSource, StringComparison.Ordinal);

        var alias = Run(AliasSource(), includeEditorReferences: true);
        AssertValid(alias);
        Assert.Contains("CurrentSerializedPayload", alias.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("OldSerializedPayload", alias.GeneratedSource, StringComparison.Ordinal);

#if SPIRE_UNITY_TEST_HOST
        var editorAssembly = Load(editor, "concrete wrapper inspector fallback");
        Assert.NotNull(editorAssembly.GetType(
            "ConcreteWrapperShapeTypes+SerializedPayloads+MoveSerializedPayload",
            throwOnError: true));
        var aliasAssembly = Load(alias, "former concrete wrapper inspector fallback");
        Assert.NotNull(aliasAssembly.GetType(
            "ConcreteWrapperAliasTypes+SerializedPayloads+OldSerializedPayload",
            throwOnError: true));
#endif
    }

    // ── Reflection and symbol helpers ────────────────────────────────────────

    private static void AssertValid(UnityGeneratorRun result)
    {
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
    }

    private static UnityGeneratorRun Run(
        string source,
        bool includeEditorReferences = false,
        LanguageVersion languageVersion = LanguageVersion.Preview)
        => UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: includeEditorReferences,
            languageVersion: languageVersion);

    private static INamedTypeSymbol RequireType(
        UnityGeneratorRun result,
        string metadataName)
        => Assert.IsAssignableFrom<INamedTypeSymbol>(
            result.OutputCompilation.GetTypeByMetadataName(metadataName));

    private static INamedTypeSymbol RequireType(Compilation compilation, string metadataName)
        => Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation.GetTypeByMetadataName(metadataName));

    private static void AssertWrapperSurface(
        INamedTypeSymbol wrapper,
        INamedTypeSymbol shared,
        INamedTypeSymbol payload)
    {
        Assert.Equal(Accessibility.Internal, wrapper.DeclaredAccessibility);
        Assert.True(wrapper.IsReferenceType);
        Assert.True(wrapper.IsSealed);
        Assert.True(HasAttribute(wrapper, "System.SerializableAttribute"));
        Assert.True(HasAttribute(wrapper, "UnityEngine.Scripting.PreserveAttribute"));
        Assert.NotNull(wrapper.BaseType);
        Assert.True(SymbolEqualityComparer.Default.Equals(
            wrapper.BaseType!.OriginalDefinition,
            shared));
        Assert.True(SymbolEqualityComparer.Default.Equals(
            wrapper.BaseType.TypeArguments.Single(),
            payload));

        var constructor = wrapper.InstanceConstructors
            .Where(candidate => !candidate.IsImplicitlyDeclared)
            .Single(candidate => candidate.Parameters.Length == 0);
        Assert.Equal(Accessibility.Public, constructor.DeclaredAccessibility);
        Assert.True(HasAttribute(constructor, "UnityEngine.Scripting.PreserveAttribute"));
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal));

    private static IFieldSymbol[] DeclaredInstanceFields(INamedTypeSymbol type)
        => type.GetMembers().OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic)
            .ToArray();

    private static FieldInfo[] DeclaredRuntimeInstanceFields(Type type)
        => type.GetFields(InstanceMembers | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsStatic)
            .ToArray();

    private static string[] InstanceFieldShapes(INamedTypeSymbol type)
        => DeclaredInstanceFields(type)
            .Select(field => field.Name + ":" + field.Type.ToDisplayString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string WrapperMetadata(string unionName, string variantName)
        => unionName + "Types+SerializedPayloads+" + variantName + "SerializedPayload";

    private static string[] WrapperNames(UnityGeneratorRun result)
        => AllNamedTypes(result.OutputCompilation)
            .Where(type => type.ContainingType?.Name == "SerializedPayloads" &&
                type.Name.EndsWith("SerializedPayload", StringComparison.Ordinal))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<ITypeParameterSymbol> AllTypeParameters(INamedTypeSymbol type)
    {
        foreach (var parameter in type.TypeParameters)
            yield return parameter;
        if (type.ContainingType is not null)
        {
            foreach (var parameter in AllTypeParameters(type.ContainingType))
                yield return parameter;
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(Compilation compilation)
        => AllNamedTypes(compilation.Assembly.GlobalNamespace);

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(INamespaceSymbol symbol)
    {
        foreach (var type in symbol.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in AllNamedTypes(type))
                yield return nested;
        }

        foreach (var child in symbol.GetNamespaceMembers())
        {
            foreach (var nested in AllNamedTypes(child))
                yield return nested;
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in AllNamedTypes(nested))
                yield return child;
        }
    }

    private static Assembly Load(UnityGeneratorRun result, string description)
        => UnityGeneratorTestHarness.EmitAndLoad(result.OutputCompilation, description);

    private static object InvokeFactory(Type unionType, string methodName, params object?[] arguments)
        => unionType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, arguments)!;

    private static object InvokeInstance(object value, string methodName)
        => value.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(value, null)!;

    private static object InvokeRestore(Type unionType, object payload)
        => unionType.GetMethod(
            "__SpireFromSerialized",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, new[] { payload })!;

    private static object? GetSerializedPayload(object union)
        => FindField(union.GetType(), "_serializedPayload")?.GetValue(union);

    private static void SetSerializedPayload(object union, object payload)
        => FindField(union.GetType(), "_serializedPayload")!.SetValue(union, payload);

    private static bool IsStateCurrent(object union, object? payload)
    {
        var method = union.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == "__SpireSerializedStateCurrent" &&
                candidate.GetParameters().Length == 1 &&
                candidate.GetParameters()[0].ParameterType == typeof(object));
        return (bool)method.Invoke(union, new[] { payload })!;
    }

    private static object AssertWrapper(object union, string expectedMetadataName)
    {
        var wrapper = GetSerializedPayload(union);
        Assert.NotNull(wrapper);
        Assert.Equal(expectedMetadataName, wrapper!.GetType().FullName);
        return wrapper;
    }

    private static object GetPayloadValue(object wrapper)
        => FindField(wrapper.GetType(), "value")!.GetValue(wrapper)!;

    private static object? ReadPayloadField(object union, string fieldName)
    {
        var wrapper = GetSerializedPayload(union);
        Assert.NotNull(wrapper);
        return ReadMember(GetPayloadValue(wrapper!), fieldName);
    }

    private static object? ReadMember(object value, string memberName)
    {
        var property = value.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is not null)
            return property.GetValue(value);

        var field = FindField(value.GetType(), "_" + memberName) ??
            FindField(value.GetType(), memberName);
        Assert.NotNull(field);
        return field!.GetValue(value);
    }

    private static string ReadKind(object union)
        => union.GetType().GetProperty("kind", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(union)!
            .ToString()!;

    private static object CreatePayload(Type payloadType, params object?[] arguments)
        => Activator.CreateInstance(
            payloadType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: arguments,
            culture: null)!;

    private static object WrapPayload(Assembly assembly, Type payloadType, object payload)
    {
        var wrapperType = assembly.GetType(SharedPayloadMetadataName, throwOnError: true)!
            .MakeGenericType(payloadType);
        var wrapper = Activator.CreateInstance(
            wrapperType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: Array.Empty<object>(),
            culture: null)!;
        FindField(wrapperType, "value")!.SetValue(wrapper, payload);
        return wrapper;
    }

    private static object CreateConcreteWrapper(
        Assembly assembly,
        string metadataName,
        object payload)
    {
        var wrapperType = assembly.GetType(metadataName, throwOnError: true)!;
        var wrapper = Activator.CreateInstance(
            wrapperType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: Array.Empty<object>(),
            culture: null)!;
        FindField(wrapperType, "value")!.SetValue(wrapper, payload);
        return wrapper;
    }

    private static FieldInfo? FindField(Type type, string fieldName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(fieldName, InstanceMembers | BindingFlags.DeclaredOnly);
            if (field is not null)
                return field;
        }

        return null;
    }

    private static void AssertChildHasCanonicalPayload(object child)
    {
        var payload = GetSerializedPayload(child);
        Assert.NotNull(payload);
        Assert.Equal(
            "ConcreteWrapperChildTypes+SerializedPayloads+ValueSerializedPayload",
            payload!.GetType().FullName);
    }

    private static string ShapeSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct ConcreteWrapperShape
        {
            [Variant] public static partial ConcreteWrapperShape Move(int destination, string label);
            [Variant] public static partial ConcreteWrapperShape Speak(string speech);
            [Variant] public static partial ConcreteWrapperShape Empty();
        }
        """;

    private static string AliasSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct ConcreteWrapperAlias
        {
            [Variant(FormerlySerializedAs = "Old")]
            public static partial ConcreteWrapperAlias Current(int value);
            [Variant(FormerlySerializedAs = "PriorEmpty")]
            public static partial ConcreteWrapperAlias Empty();
            [Variant] public static partial ConcreteWrapperAlias Other(string label);
        }
        """;

    private static string NestedSource() => """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct ConcreteWrapperChild
        {
            [Variant] public static partial ConcreteWrapperChild Value(int value);
        }

        [Serializable, DiscriminatedUnion]
        public partial struct ConcreteWrapperNested
        {
            [Variant] public static partial ConcreteWrapperNested Child(ConcreteWrapperChild child);
            [Variant] public static partial ConcreteWrapperNested Array(ConcreteWrapperChild[] children);
            [Variant] public static partial ConcreteWrapperNested List(List<ConcreteWrapperChild> children);
        }
        """;

    private static string ScopeSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        namespace Deep.Layer
        {
            [Serializable, DiscriminatedUnion]
            public partial struct ConcreteWrapperLeft
            {
                [Variant] public static partial ConcreteWrapperLeft Move(int value);
                [Variant] public static partial ConcreteWrapperLeft Empty();
            }

            [Serializable, DiscriminatedUnion]
            public partial struct ConcreteWrapperRight
            {
                [Variant] public static partial ConcreteWrapperRight Move(int value);
                [Variant] public static partial ConcreteWrapperRight Empty();
            }

            public static partial class ConcreteWrapperOwner
            {
                [Serializable, DiscriminatedUnion]
                public partial struct Nested
                {
                    [Variant] public static partial Nested Run(int value);
                    [Variant] public static partial Nested Stop();
                }

                [Serializable, DiscriminatedUnion]
                private partial struct Hidden
                {
                    [Variant] public static partial Hidden Value(int value);
                    [Variant] public static partial Hidden Empty();
                }
            }

            [Serializable, DiscriminatedUnion]
            public partial struct ConcreteWrapperEscapedNames
            {
                [Variant] public static partial ConcreteWrapperEscapedNames @event(int @class);
                [Variant] public static partial ConcreteWrapperEscapedNames Empty();
            }
        }
        """;

    private static string GenericSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct ConcreteWrapperGeneric<T>
        {
            [Variant(FormerlySerializedAs = "OldValue")]
            public static partial ConcreteWrapperGeneric<T> Value(T value);
            [Variant] public static partial ConcreteWrapperGeneric<T> Empty();
        }

        public static partial class ConcreteWrapperGenericOwner<T> where T : class
        {
            [Serializable, DiscriminatedUnion]
            public partial struct Nested
            {
                [Variant] public static partial Nested Value(T value);
                [Variant] public static partial Nested Empty();
            }
        }

        public static class ConcreteWrapperGenericUses
        {
            public static ConcreteWrapperGeneric<int> IntValue => ConcreteWrapperGeneric<int>.Value(3);
            public static ConcreteWrapperGeneric<string> StringValue => ConcreteWrapperGeneric<string>.Value("text");
        }
        """;

    private static string ControlSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct ConcreteWrapperSerializable
        {
            [Variant] public static partial ConcreteWrapperSerializable Value(int value);
        }

        [DiscriminatedUnion]
        public partial struct ConcreteWrapperPlain
        {
            [Variant] public static partial ConcreteWrapperPlain Value(int value);
        }

        [Serializable]
        public sealed class UnrelatedSerializable
        {
            public int value;
        }
        """;
}
