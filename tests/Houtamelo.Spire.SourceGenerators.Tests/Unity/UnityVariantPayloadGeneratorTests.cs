using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Structural phase-three contract for the public payload and Unity wrapper shape.
/// Native serialization, Inspector interaction, and player behavior are covered by
/// the Unity integration fixtures rather than this generator-driver suite.
public sealed class UnityVariantPayloadGeneratorTests
{
    private const string SharedDrawerTypeName =
        "Houtamelo.Spire.Generated.__SpireUnityPropertyDrawer";
    private const string DrawerMetadataTypeName =
        "Houtamelo.Spire.Generated.__SpireUnityDrawerMetadata";
    private const string VariantMetadataTypeName =
        "Houtamelo.Spire.Generated.__SpireUnityVariantMetadata";

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_UsesSiblingMutablePayloadsAndOneSharedGenericWrapper()
    {
        var result = Run(BasicSource(), includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "Shape");
        foreach (var variant in new[] { "Circle", "Move", "Empty" })
        {
            var payload = result.OutputCompilation.GetTypeByMetadataName("ShapeTypes+Variants+" + variant);
            Assert.NotNull(payload);
            Assert.True(payload!.IsValueType);
            Assert.True(HasAttribute(payload, "System.SerializableAttribute"));
            Assert.All(
                payload.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic),
                field => Assert.Equal(Accessibility.Private, field.DeclaredAccessibility));
            Assert.All(
                payload.GetMembers().OfType<IPropertySymbol>(),
                property => Assert.Null(property.SetMethod));
            Assert.Contains(payload.GetMembers("Deconstruct").OfType<IMethodSymbol>(), method =>
                method.Parameters.Length == payload.GetMembers().OfType<IFieldSymbol>().Count(field => !field.IsStatic));
        }

        var wrappers = AllNamedTypes(result.OutputCompilation)
            .Where(type => type.TypeParameters.Length == 1 &&
                type.BaseType?.ToDisplayString() ==
                    "Houtamelo.Spire.UnitySerializationPayloadBase")
            .ToArray();
        Assert.Single(wrappers);
        Assert.True(wrappers[0].IsReferenceType);

        var payloadField = FindField(union, "_serializedPayload");
        Assert.NotNull(payloadField);
        Assert.True(HasAttribute(payloadField!, "UnityEngine.SerializeReference"));
        Assert.Contains(union.GetMembers("_kind").OfType<IFieldSymbol>(), field =>
            HasAttribute(field, "UnityEngine.SerializeField"));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_PayloadFieldsCarryUnitySerializationPolicy()
    {
        var result = Run(ManagedReferencePayloadSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var payload = result.OutputCompilation.GetTypeByMetadataName("ShapeTypes+Variants+Node");
        Assert.NotNull(payload);

        var node = payload!.GetMembers("_node").OfType<IFieldSymbol>().Single();
        var count = payload.GetMembers("_count").OfType<IFieldSymbol>().Single();
        Assert.True(HasAttribute(node, "UnityEngine.SerializeReference"));
        Assert.True(HasAttribute(count, "UnityEngine.SerializeField"));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_RuntimeStorageIsNotSerializedAndFlatApiIsGone()
    {
        var result = Run(BasicSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "Shape");
        var runtimeFields = union.GetMembers().OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic && field.Name is not "_kind" and not "_serializedPayload")
            .ToArray();
        Assert.NotEmpty(runtimeFields);
        Assert.All(runtimeFields, field => Assert.True(
            HasAttribute(field, "System.NonSerializedAttribute"),
            $"Runtime field '{field.Name}' must be excluded from Unity serialization."));

        Assert.Empty(union.GetMembers("radius").OfType<IPropertySymbol>());
        Assert.Empty(union.GetMembers("destination").OfType<IPropertySymbol>());
        Assert.Empty(union.GetMembers("Deconstruct").OfType<IMethodSymbol>());
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_EmitsBridgeCallbacksAndNoEagerSnapshotFactoryAllocation()
    {
        var result = Run(BasicSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "Shape");
        foreach (var callback in new[]
        {
            "__SpirePrepareForSerialization",
            "__SpireHydrateSerialized",
            "__SpireSerializedStateCurrent",
            "__SpireFromSerialized",
            "__SpireCreateDefault",
        })
        {
            Assert.NotEmpty(union.GetMembers(callback));
        }
        foreach (var helper in new[]
        {
            "__SpirePrepareForSerialization",
            "__SpireHydrateSerialized",
            "__SpireSerializedStateCurrent",
            "__SpireFromSerialized",
            "__SpireCreateDefault",
        })
        {
            Assert.All(
                union.GetMembers(helper).OfType<IMethodSymbol>(),
                method => Assert.Equal(Accessibility.Public, method.DeclaredAccessibility));
        }
        Assert.All(
            union.GetMembers("__SpireHydrateSerialized").OfType<IMethodSymbol>(),
            method => Assert.True(method.IsReadOnly));
        AssertCallbackImplementation(union, "OnBeforeSerialize");
        AssertCallbackImplementation(union, "OnAfterDeserialize");

        var circleFactory = union.GetMembers("Circle").OfType<IMethodSymbol>().Single();
        var factorySyntax = circleFactory.DeclaringSyntaxReferences
            .Concat(circleFactory.PartialImplementationPart is { } implementation
                ? implementation.DeclaringSyntaxReferences
                : ImmutableArray<SyntaxReference>.Empty)
            .Select(reference => reference.GetSyntax().ToString());
        Assert.DoesNotContain(
            factorySyntax,
            text => text.Contains("SerializedPayload", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_GenericPayloadsPreserveConstraintsAndNestedScope()
    {
        var result = Run(GenericAndNestedSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var generic = FindUnion(result, "GenericShape`1");
        Assert.True(generic.TypeParameters[0].HasUnmanagedTypeConstraint);
        var genericPayload = result.OutputCompilation.GetTypeByMetadataName("GenericShapeTypes1+Variants+Circle`1");
        Assert.NotNull(genericPayload);
        Assert.True(genericPayload!.TypeParameters[0].HasUnmanagedTypeConstraint);

        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("Outer+CommandTypes+Variants+Run"));
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName("CommandTypes+Variants+Run"));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_FormerVariantNameEmitsConcreteCanonicalAndFormerWrappers()
    {
        var result = Run(AliasSource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var canonical = result.OutputCompilation.GetTypeByMetadataName("RenamedTypes+Variants+Current");
        var former = result.OutputCompilation.GetTypeByMetadataName("RenamedTypes+Variants+Old");
        Assert.NotNull(canonical);
        Assert.NotNull(former);
        Assert.True(canonical!.IsValueType);
        Assert.True(former!.IsValueType);
        Assert.Contains("Old", result.GeneratedSource, StringComparison.Ordinal);

        var shared = Assert.IsAssignableFrom<INamedTypeSymbol>(
            result.OutputCompilation.GetTypeByMetadataName(
                "Houtamelo.Spire.Generated.SpireSerializedPayload`1"));
        var canonicalWrapper = result.OutputCompilation.GetTypeByMetadataName(
            "RenamedTypes+SerializedPayloads+CurrentSerializedPayload");
        var formerWrapper = result.OutputCompilation.GetTypeByMetadataName(
            "RenamedTypes+SerializedPayloads+OldSerializedPayload");
        AssertConcreteWrapper(canonicalWrapper, shared, canonical);
        AssertConcreteWrapper(formerWrapper, shared, former);
        Assert.Contains("CurrentSerializedPayload", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("OldSerializedPayload", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_SharedEditorSupportIsOneGuardedSourceWithThinAdapters()
    {
        var result = Run(BasicSource(), includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var supportType = result.OutputCompilation.GetTypeByMetadataName(SharedDrawerTypeName);
        Assert.NotNull(supportType);
        Assert.True(supportType!.IsAbstract);
        Assert.Equal(
            "UnityEditor.PropertyDrawer",
            supportType.BaseType?.ToDisplayString());

        var supportTrees = EditorSupportTrees(result);
        Assert.Single(supportTrees);
        var supportSyntax = ParseEditorSource(supportTrees[0].GetText().ToString());
        Assert.True(
            ContainsFindPropertyRelativeLiteral(supportSyntax, "value"),
            "The shared drawer must bind the serialized wrapper's value field through a syntax-level property lookup.");
        Assert.Contains(
            supportSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => IsMemberInvocation(invocation, "PropertyField"));

        var adapters = FindPropertyDrawers(result).ToArray();
        Assert.NotEmpty(adapters);
        Assert.All(adapters, adapter =>
        {
            Assert.True(
                SymbolEqualityComparer.Default.Equals(adapter.BaseType, supportType),
                $"Drawer '{adapter.ToDisplayString()}' must derive from the shared support type.");
            Assert.DoesNotContain(
                adapter.GetMembers().OfType<IMethodSymbol>(),
                method => method.Name is "CreatePropertyGUI" or "OnGUI" or "GetPropertyHeight");
        });
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnions_EmitOneWrapperAndOneEditorSupportPerCompilation()
    {
        var source = TwoSerializableUnionsSource();
        var editorResult = Run(source, includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(editorResult);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(editorResult);

        var wrappers = AllNamedTypes(editorResult.OutputCompilation)
            .Where(type => type.TypeParameters.Length == 1 &&
                type.BaseType?.ToDisplayString() ==
                    "Houtamelo.Spire.UnitySerializationPayloadBase")
            .ToArray();
        Assert.Single(wrappers);

        var supportTrees = EditorSupportTrees(editorResult);
        Assert.Single(supportTrees);

        var playerResult = Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(playerResult);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(playerResult);
        var playerWrappers = AllNamedTypes(playerResult.OutputCompilation)
            .Where(type => type.TypeParameters.Length == 1 &&
                type.BaseType?.ToDisplayString() ==
                    "Houtamelo.Spire.UnitySerializationPayloadBase")
            .ToArray();
        Assert.Single(playerWrappers);
        Assert.Null(playerResult.OutputCompilation.GetTypeByMetadataName(SharedDrawerTypeName));
        Assert.Null(playerResult.OutputCompilation.GetTypeByMetadataName(DrawerMetadataTypeName));
        Assert.Null(playerResult.OutputCompilation.GetTypeByMetadataName(VariantMetadataTypeName));
        Assert.Empty(FindPropertyDrawers(playerResult));
        Assert.DoesNotContain(
            playerResult.OutputCompilation.References,
            reference => string.Equals(
                System.IO.Path.GetFileName(reference.Display),
                "UnityEditor.dll",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SharedEditorTemplate_IsAnEmbeddedCSharpSourceResource()
    {
        var generatorAssembly = typeof(DiscriminatedUnionGenerator).Assembly;
        var resources = generatorAssembly
            .GetManifestResourceNames()
            .Where(name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(name => (Name: name, Stream: generatorAssembly.GetManifestResourceStream(name)))
            .Where(item => item.Stream is not null)
            .Select(item =>
            {
                using var reader = new System.IO.StreamReader(item.Stream!);
                return (item.Name, Text: reader.ReadToEnd());
            })
            .Where(item => item.Text.Contains("PropertyField", StringComparison.Ordinal) &&
                item.Text.Contains("FindPropertyRelative", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(resources);
        var syntax = CSharpSyntaxTree.ParseText(
            resources[0].Text,
            EditorParseOptions(),
            path: resources[0].Name);
        Assert.DoesNotContain(syntax.GetDiagnostics(), diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);

        var templateCompilation = CSharpCompilation.Create(
            "SpireEmbeddedUnityDrawerTemplate",
            new[] { syntax },
            UnityGeneratorTestHarness.GetReferences(
                includeUnityReferences: true,
                includeEditorReferences: true),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.DoesNotContain(
            templateCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableUnion_GenericAncestorDiagnosticKeepsPayloadApi()
    {
        var result = Run(GenericAncestorSource());
        Assert.Contains(result.GeneratorDiagnostics, diagnostic =>
            diagnostic.Id == "SPIRE_DU018" &&
            diagnostic.GetMessage().Contains("Container", StringComparison.Ordinal));

        var union = FindUnion(result, "Container`1+Command");
        Assert.NotEmpty(union.GetMembers("Run"));
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("Container`1+CommandTypes+Variants+Run"));
    }

    [Fact]
    public void SerializableUnion_MissingUnityReferencesRetainsCoreApiWithoutUnityOptIn()
    {
        var result = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;
            [Serializable, DiscriminatedUnion]
            public partial struct CoreOnly
            {
                [Variant] public static partial CoreOnly Value(int value);
            }
            """,
            includeUnityReferences: false);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        var union = FindUnion(result, "CoreOnly");
        Assert.NotEmpty(union.GetMembers("Value"));
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("CoreOnlyTypes+Variants+Value"));
        Assert.DoesNotContain(
            "ISerializationCallbackReceiver",
            result.GeneratedSource,
            StringComparison.Ordinal);
    }

    private static UnityGeneratorRun Run(string source, bool includeEditorReferences = false)
        => UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: includeEditorReferences);

    private static INamedTypeSymbol FindUnion(UnityGeneratorRun result, string metadataName)
        => Assert.IsAssignableFrom<INamedTypeSymbol>(
            result.OutputCompilation.GetTypeByMetadataName(metadataName));

    private static IFieldSymbol? FindField(INamedTypeSymbol type, string name)
        => type.GetMembers(name).OfType<IFieldSymbol>().SingleOrDefault();

    private static bool HasAttribute(ISymbol symbol, string fullName)
        => symbol.GetAttributes().Any(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), fullName, StringComparison.Ordinal));

    private static void AssertConcreteWrapper(
        INamedTypeSymbol? wrapper,
        INamedTypeSymbol shared,
        INamedTypeSymbol payload)
    {
        Assert.NotNull(wrapper);
        var concrete = wrapper!;
        Assert.Equal(Accessibility.Internal, concrete.DeclaredAccessibility);
        Assert.True(concrete.IsReferenceType);
        Assert.True(concrete.IsSealed);
        Assert.True(HasAttribute(concrete, "System.SerializableAttribute"));
        Assert.True(HasAttribute(concrete, "UnityEngine.Scripting.PreserveAttribute"));
        Assert.True(SymbolEqualityComparer.Default.Equals(
            concrete.BaseType!.OriginalDefinition,
            shared));
        Assert.True(SymbolEqualityComparer.Default.Equals(
            concrete.BaseType.TypeArguments.Single(),
            payload));
        Assert.DoesNotContain(
            concrete.GetMembers().OfType<IFieldSymbol>(),
            field => !field.IsStatic);
    }

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

    private static SyntaxNode ParseEditorSource(string source, string path = "__SpireUnityDrawerSupport.cs")
        => CSharpSyntaxTree.ParseText(source, EditorParseOptions(), path).GetRoot();

    private static CSharpParseOptions EditorParseOptions()
        => CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("UNITY_EDITOR");

    private static SyntaxTree[] EditorSupportTrees(UnityGeneratorRun result)
        => result.RunResult.GeneratedTrees
            .Where(tree => ParseEditorSource(tree.GetText().ToString(), tree.FilePath)
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Any(type => type.Identifier.ValueText == "__SpireUnityPropertyDrawer"))
            .ToArray();

    private static IEnumerable<INamedTypeSymbol> FindPropertyDrawers(UnityGeneratorRun result)
        => AllNamedTypes(result.OutputCompilation)
            .Where(type => type.GetAttributes().Any(IsCustomPropertyDrawer));

    private static bool IsCustomPropertyDrawer(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "UnityEditor.CustomPropertyDrawer";

    private static bool ContainsFindPropertyRelativeLiteral(SyntaxNode root, string value)
    {
        return root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsMemberInvocation(invocation, "FindPropertyRelative"))
            .Any(invocation => invocation.ArgumentList.Arguments.Count > 0 &&
                invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                string.Equals(literal.Token.ValueText, value, StringComparison.Ordinal));
    }

    private static bool IsMemberInvocation(InvocationExpressionSyntax invocation, string memberName)
        => invocation.Expression is MemberAccessExpressionSyntax member &&
            member.Name.Identifier.ValueText == memberName;

    private static INamedTypeSymbol[] AllNamedTypes(Compilation compilation)
        => FlattenNamespace(compilation.Assembly.GlobalNamespace)
            .SelectMany(FlattenTypes)
            .ToArray();

    private static IEnumerable<INamedTypeSymbol> FlattenNamespace(INamespaceSymbol symbol)
    {
        foreach (var type in symbol.GetTypeMembers())
            yield return type;
        foreach (var child in symbol.GetNamespaceMembers())
        {
            foreach (var type in FlattenNamespace(child))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> FlattenTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers().SelectMany(FlattenTypes))
            yield return nested;
    }

    private static string BasicSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        [Serializable, DiscriminatedUnion]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(float radius);
            [Variant] public static partial Shape Move(Vector2 destination, float speed);
            [Variant] public static partial Shape Empty();
        }
        """;

    private static string ManagedReferencePayloadSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        public sealed class NodeBase { public int id; }
        [Serializable, DiscriminatedUnion]
        public partial struct Shape
        {
            [Variant] public static partial Shape Node([UnitySerializeReference] NodeBase node, int count);
        }
        """;

    private static string GenericAndNestedSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        [Serializable, DiscriminatedUnion]
        public partial struct GenericShape<T> where T : unmanaged
        {
            [Variant] public static partial GenericShape<T> Circle(T radius);
        }
        public static partial class Outer
        {
            [Serializable, DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(int value);
            }
        }
        """;

    private static string AliasSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        [Serializable, DiscriminatedUnion]
        public partial struct Renamed
        {
            [Variant(FormerlySerializedAs = "Old")] public static partial Renamed Current(int value);
        }
        """;

    private static string TwoSerializableUnionsSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        [Serializable, DiscriminatedUnion]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(float radius);
        }
        [Serializable, DiscriminatedUnion]
        public partial struct ColorChoice
        {
            [Variant] public static partial ColorChoice Rgb(Color value);
        }
        """;

    private static string GenericAncestorSource() => """
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        public class Container<T>
        {
            [Serializable, DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(int value);
            }
        }
        """;
}
