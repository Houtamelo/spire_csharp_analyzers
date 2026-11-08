using System;
using System.Linq;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadableFactories;

[Trait("Environment", "Unity")]
public sealed class UnityGeneratedCodeReadabilityTests
{
    [Fact]
    public void SerializableOutputUsesAliasesAndFullyQualifiedPayloadWrappers()
    {
        var result = UnityGeneratorTestHarness.Run(Source(), includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var source = result.GeneratedSource;
        Assert.Contains("global::Houtamelo.Spire.IDiscriminatedUnion<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("using IDiscriminatedUnion =", source, StringComparison.Ordinal);
        foreach (var alias in new[]
        {
            "using SpireSerialization = global::Houtamelo.Spire.UnitySerializationBridge;",
            "using SerializeField = global::UnityEngine.SerializeField;",
            "using Serializable = global::System.SerializableAttribute;",
            "using Preserve = global::UnityEngine.Scripting.PreserveAttribute;",
            "using NonSerialized = global::System.NonSerializedAttribute;",
            "using StructLayout = global::System.Runtime.InteropServices.StructLayoutAttribute;",
            "using LayoutKind = global::System.Runtime.InteropServices.LayoutKind;",
            "using ISerializationCallbackReceiver = global::UnityEngine.ISerializationCallbackReceiver;",
            "using EditorBrowsable = global::System.ComponentModel.EditorBrowsableAttribute;",
            "using EditorBrowsableState = global::System.ComponentModel.EditorBrowsableState;",
        })
        {
            Assert.Contains(alias, source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("using SerializedPayload_", source, StringComparison.Ordinal);
        Assert.Contains(
            "global::Houtamelo.Spire.Generated.SpireSerializedPayload<Variants.Current>",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::Houtamelo.Spire.Generated.SpireSerializedPayload<Variants.Old>",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("global::Houtamelo.Spire.Generated.__SpireSerializedPayload<", source, StringComparison.Ordinal);

        Assert.DoesNotContain(".__SpireSerializedValuesEqual", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".__SpireSerializedManagedReferenceValuesEqual", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".__SpireCloneSerializedSnapshot", source, StringComparison.Ordinal);
        var compactSource = new string(source.Where(character => !char.IsWhiteSpace(character)).ToArray());
        Assert.Contains("SpireSerialization.SpireSerializedValuesEqual", compactSource, StringComparison.Ordinal);
        Assert.Contains("SpireSerialization.SpireCloneSerializedSnapshot", compactSource, StringComparison.Ordinal);

        var genericWrapper = result.OutputCompilation.GetTypeByMetadataName(
            "Houtamelo.Spire.Generated.SpireSerializedPayload`1");
        Assert.NotNull(genericWrapper);
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName(
            "Houtamelo.Spire.Generated.__SpireSerializedPayload`1"));
    }

    [Fact]
    public void GenericPayloadReferencesStayGloballyQualifiedAndCallbacksAreExplicit()
    {
        var result = UnityGeneratorTestHarness.Run(Source());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var source = result.GeneratedSource;
        Assert.DoesNotContain("using SerializedPayload_", source, StringComparison.Ordinal);
        Assert.Contains(
            "global::Houtamelo.Spire.Generated.SpireSerializedPayload<Variants.Value<T>>",
            source,
            StringComparison.Ordinal);

        var union = result.OutputCompilation.GetTypeByMetadataName("Readability.Shape")!;
        AssertCallbackImplementation(union, "OnBeforeSerialize");
        AssertCallbackImplementation(union, "OnAfterDeserialize");

        var payload = result.OutputCompilation.GetTypeByMetadataName("Readability.ShapeTypes+Variants+Current")!;
        var field = payload.GetMembers("_value").OfType<IFieldSymbol>().Single();
        Assert.Equal(Accessibility.Private, field.DeclaredAccessibility);

        var unionSource = result.RunResult.GeneratedTrees
            .Select(tree => tree.GetText().ToString())
            .Single(text => text.Contains("struct Current", StringComparison.Ordinal));
        var root = CSharpSyntaxTree.ParseText(unionSource).GetRoot();
        Assert.DoesNotContain(
            root.DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .SelectMany(member => member.Modifiers),
            modifier => modifier.IsKind(SyntaxKind.PrivateKeyword));
    }

    [Fact]
    public void SerializedPayloadRestoreUsesASwitchInsteadOfPayloadTypeIfChains()
    {
        var result = UnityGeneratorTestHarness.Run(Source());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var root = GeneratedUnionRoot(result, "Shape");
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == "__SpireTryFromSerialized");

        Assert.Contains(method.Body!.Statements, statement => statement is SwitchStatementSyntax);
        Assert.DoesNotContain(
            method.Body.DescendantNodes().OfType<IfStatementSyntax>(),
            statement => statement.Condition.ToString().Contains("payload", StringComparison.Ordinal));
    }

    [Fact]
    public void UnityCallbacksUseKAndRBraces()
    {
        var result = UnityGeneratorTestHarness.Run(Source());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var source = result.GeneratedSource;
        Assert.Contains("OnAfterDeserialize() {", source, StringComparison.Ordinal);
        Assert.Contains("if (!validPayload) {", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnAfterDeserialize()\n{", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!validPayload)\n{", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UnionIsTheFirstTopLevelTypeInItsGeneratedFile()
    {
        var result = UnityGeneratorTestHarness.Run(Source());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var root = GeneratedUnionRoot(result, "Shape");
        var namespaceDeclaration = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .Single();
        var firstType = namespaceDeclaration.Members
            .OfType<TypeDeclarationSyntax>()
            .First();

        Assert.Equal("Shape", firstType.Identifier.ValueText);
    }

    [Fact]
    public void NestedUnionIsTheFirstTypeInsideItsContainingType()
    {
        var result = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;

            public static partial class Outer
            {
                public static partial class Inner
                {
                    [Serializable, DiscriminatedUnion]
                    public partial struct Shape
                    {
                        [Variant]
                        public static partial Shape Circle(float radius);
                    }
                }
            }
            """);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var root = GeneratedUnionRoot(result, "Shape");
        var inner = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(type => type.Identifier.ValueText == "Inner");
        var firstType = inner.Members
            .OfType<TypeDeclarationSyntax>()
            .First();

        Assert.Equal("Shape", firstType.Identifier.ValueText);
    }

    [Fact]
    public void AllGeneratedSourcesUseKAndROpeningBraces()
    {
        var result = UnityGeneratorTestHarness.Run(Source(), includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var tree in result.RunResult.GeneratedTrees)
        {
            var standaloneOpeningBraces = tree.GetText()
                .Lines
                .Select(line => line.ToString().Trim())
                .Count(line => line == "{");
            Assert.True(
                standaloneOpeningBraces == 0,
                $"Generated source '{tree.FilePath}' contains {standaloneOpeningBraces} standalone opening brace(s).");
        }
    }

    [Fact]
    public void NamespaceContentsHaveNoExtraBaseIndentInUnityGeneratedSources()
    {
        foreach (var source in new[] { Source(), NestedSource() })
        {
            var result = UnityGeneratorTestHarness.Run(source, includeEditorReferences: true);
            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

            foreach (var tree in result.RunResult.GeneratedTrees)
                GeneratedIndentationAssertions.AssertNamespaceMembersHaveNoBaseIndent(tree);
        }
    }

    [Fact]
    public void ImplicitlyPrivateContainingTypesUseFullyQualifiedPayloadWrappers()
    {
        var result = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;

            namespace Readability
            {
                public static partial class Outer
                {
                    partial class Inner
                    {
                        [Serializable]
                        [DiscriminatedUnion]
                        public partial struct Shape
                        {
                            [Variant]
                            public static partial Shape Value(int value);
                        }
                    }
                }
            }
            """);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        Assert.DoesNotContain("using SerializedPayload_", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(
            "global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Readability.Outer.Inner.ShapeTypes.Variants.Value>",
            result.GeneratedSource,
            StringComparison.Ordinal);
        Assert.NotNull(result.OutputCompilation.GetTypeByMetadataName("Readability.Outer+Inner+Shape"));
    }

    private static void AssertCallbackImplementation(INamedTypeSymbol union, string callbackName)
    {
        var callbackInterface = union.AllInterfaces.Single(interfaceType =>
            interfaceType.ToDisplayString() == "UnityEngine.ISerializationCallbackReceiver");
        var interfaceMember = callbackInterface.GetMembers(callbackName)
            .OfType<IMethodSymbol>()
            .Single();
        var implementation = union.FindImplementationForInterfaceMember(interfaceMember);
        Assert.Equal(
            MethodKind.ExplicitInterfaceImplementation,
            Assert.IsAssignableFrom<IMethodSymbol>(implementation).MethodKind);
    }

    private static SyntaxNode GeneratedUnionRoot(UnityGeneratorRun result, string unionName)
        => result.RunResult.GeneratedTrees
            .Select(tree => tree.GetRoot())
            .Single(root => root.DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(structure => structure.Identifier.ValueText == unionName));

    private static string Source() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        namespace Readability
        {
            [Serializable]
            [DiscriminatedUnion]
            public partial struct Shape
            {
                [Variant(FormerlySerializedAs = "Old")]
                public static partial Shape Current(int value);

                [Variant]
                public static partial Shape Idle();
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct Generic<T> where T : unmanaged
            {
                [Variant]
                public static partial Generic<T> Value(T value);

                [Variant]
                public static partial Generic<T> Empty();
            }
        }
        """;

    private static string NestedSource() => """
        using System;
        using Houtamelo.Spire;

        namespace Readability
        {
            public static partial class Outer
            {
                public static partial class Inner
                {
                    [Serializable, DiscriminatedUnion]
                    public partial struct NestedShape
                    {
                        [Variant]
                        public static partial NestedShape Circle(float radius);
                    }
                }
            }
        }
        """;
}
