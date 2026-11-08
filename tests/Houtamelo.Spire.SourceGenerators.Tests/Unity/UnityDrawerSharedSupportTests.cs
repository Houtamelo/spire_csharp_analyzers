using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnityDrawerSharedSupportTests
{
    private const string SupportHint = "Spire.UnityDrawerSupport.generated.cs";
    private const string SupportTypeName =
        "Houtamelo.Spire.Generated.__SpireUnityPropertyDrawer";
    private const string MetadataTypeName =
        "Houtamelo.Spire.Generated.__SpireUnityDrawerMetadata";
    private const string VariantMetadataTypeName =
        "Houtamelo.Spire.Generated.__SpireUnityVariantMetadata";

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_EmitsOneSharedSupportTreePerCompilation()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_AllLayouts"),
            includeEditorReferences: true);

        AssertValidEditorCompilation(result);
        Assert.Single(SupportTrees(result));

        var support = RequireType(result, SupportTypeName);
        var metadata = RequireType(result, MetadataTypeName);
        var variantMetadata = RequireType(result, VariantMetadataTypeName);
        Assert.Equal(Accessibility.Internal, support.DeclaredAccessibility);
        Assert.True(support.IsAbstract);
        Assert.Equal(Accessibility.Internal, metadata.DeclaredAccessibility);
        Assert.True(metadata.IsSealed);
        Assert.Equal(Accessibility.Internal, variantMetadata.DeclaredAccessibility);
        Assert.True(variantMetadata.IsReadOnly);
        Assert.Equal(TypeKind.Struct, variantMetadata.TypeKind);

        foreach (var metadataName in new[]
        {
            "StabilityNestedChildUnion",
            "StabilityAdditiveUnion",
            "StabilityBoxedFieldsUnion",
            "StabilityBoxedTupleUnion",
            "StabilityUnsafeOverlapUnion",
            "StabilityOverlapUnion",
        })
        {
            var union = RequireType(result, metadataName);
            var drawer = FindDrawerForTarget(result, union);
            AssertThinWrapper(drawer, metadata);
            AssertMetadataDeclaredUnionTypeMatches(result, drawer, union);
        }

        Assert.Equal(6, FindDrawers(result).Count());
        Assert.Single(
            GetAllTypes(result.OutputCompilation.GlobalNamespace),
            type => type.ToDisplayString() == SupportTypeName);
        Assert.Single(
            GetAllTypes(result.OutputCompilation.GlobalNamespace),
            type => type.ToDisplayString() == MetadataTypeName);
        Assert.Single(
            GetAllTypes(result.OutputCompilation.GlobalNamespace),
            type => type.ToDisplayString() == VariantMetadataTypeName);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_SupportHintIsStableAndCompilationScoped()
    {
        var oneUnion = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Drawer_Basic"),
            includeEditorReferences: true);
        var twoUnions = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Drawer_Basic") +
            "\n" +
            """
            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnitySecondDrawer
            {
                [Variant] public static partial UnitySecondDrawer Value(int value);
                [Variant] public static partial UnitySecondDrawer Empty();
            }
            """,
            includeEditorReferences: true);

        AssertValidEditorCompilation(oneUnion);
        AssertValidEditorCompilation(twoUnions);

        Assert.Equal(SupportHint, Path.GetFileName(Assert.Single(SupportTrees(oneUnion)).FilePath));
        Assert.Equal(SupportHint, Path.GetFileName(Assert.Single(SupportTrees(twoUnions)).FilePath));
        Assert.Single(SupportTrees(oneUnion));
        Assert.Single(SupportTrees(twoUnions));
        Assert.Equal(2, FindDrawers(twoUnions).Count());
        Assert.DoesNotContain(
            twoUnions.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Id is "CS0101" or "CS0111" or "CS0436");
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_MetadataPayloadTypesDistinguishUnionsWithTheSameVariantNames()
    {
        var result = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct FirstCommand
            {
                [Variant(FormerlySerializedAs = "OldMove")]
                public static partial FirstCommand Move(int value);
            }

            [Serializable, DiscriminatedUnion]
            public partial struct SecondCommand
            {
                [Variant(FormerlySerializedAs = "OldMove")]
                public static partial SecondCommand Move(int value);
            }
            """,
            includeEditorReferences: true);
        AssertValidEditorCompilation(result);

        var first = RequireType(result, "FirstCommand");
        var second = RequireType(result, "SecondCommand");
        var firstRows = ReadMetadataVariants(result, FindDrawerForTarget(result, first));
        var secondRows = ReadMetadataVariants(result, FindDrawerForTarget(result, second));

        Assert.Equal("Move", Assert.Single(firstRows).Ident);
        Assert.Equal("Move", Assert.Single(secondRows).Ident);
        Assert.NotEqual(firstRows[0].PayloadType, secondRows[0].PayloadType);
        Assert.Contains("FirstCommandTypes", firstRows[0].PayloadType, StringComparison.Ordinal);
        Assert.Contains("SecondCommandTypes", secondRows[0].PayloadType, StringComparison.Ordinal);
        Assert.Contains("FirstCommandTypes", firstRows[0].PayloadTypeAlias!, StringComparison.Ordinal);
        Assert.Contains("SecondCommandTypes", secondRows[0].PayloadTypeAlias!, StringComparison.Ordinal);
        Assert.NotEqual(firstRows[0].PayloadTypeAlias, secondRows[0].PayloadTypeAlias);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_WrapperIdentityAndTargetArePreserved()
    {
        foreach (var (fixture, metadataName, wrapperName) in new[]
        {
            ("Drawer_Basic", "UnityDrawer", "__SpireUnityPropertyDrawer_UnityDrawer"),
            (
                "Serializable_ExplicitBoxedFields",
                "UnityBoxedFieldsUnion",
                "__SpireUnityPropertyDrawer_UnityBoxedFieldsUnion"),
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixture),
                includeEditorReferences: true);
            AssertValidEditorCompilation(result);

            var union = RequireType(result, metadataName);
            var drawer = FindDrawerForTarget(result, union);
            Assert.Equal(wrapperName, drawer.Name);
            Assert.Null(drawer.ContainingType);
            AssertThinWrapper(drawer, RequireType(result, MetadataTypeName));
        }
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_MetadataIsCompleteAndImmutable()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_Aliases"),
            includeEditorReferences: true);
        AssertValidEditorCompilation(result);

        var union = RequireType(result, "StabilityMigrationUnion");
        var drawer = FindDrawerForTarget(result, union);
        var metadataType = RequireType(result, MetadataTypeName);
        AssertMetadataShape(metadataType, RequireType(result, VariantMetadataTypeName));
        Assert.True(SymbolEqualityComparer.Default.Equals(metadataType, MetadataField(drawer).Type));

        AssertVariantRowsEqual(
            new (
                string Ident,
                string PayloadType,
                string? PayloadTypeAlias,
                IReadOnlyList<string> Fields)[]
            {
                ("Renamed", "StabilityMigrationUnionTypes+Variants+Renamed", "StabilityMigrationUnionTypes+Variants+OldRenamed",
                    new[] { "_amount", "_label", "_count", "_pair", "_asset", "_child", "_values" }),
                ("Empty", "StabilityMigrationUnionTypes+Variants+Empty", "StabilityMigrationUnionTypes+Variants+OldEmpty", Array.Empty<string>()),
                ("Other", "StabilityMigrationUnionTypes+Variants+Other", null, new[] { "_otherLabel" }),
            },
            ReadMetadataVariants(result, drawer));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_OpenGenericMetadataUsesNullUnionType()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Generic_IntString"),
            includeEditorReferences: true);
        AssertValidEditorCompilation(result);

        var union = RequireType(result, "UnityGeneric`1");
        var drawer = FindDrawerForTarget(result, union, requireUnboundGenericTarget: true);
        Assert.False(drawer.IsGenericType);
        AssertMetadataDeclaredUnionTypeIsNull(result, drawer);
        AssertMetadataShape(
            RequireType(result, MetadataTypeName),
            RequireType(result, VariantMetadataTypeName));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_PrivateNestedWrapperPreservesContainingType()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            public partial class PrivateDrawerContainer
            {
                [Serializable]
                [DiscriminatedUnion]
                private partial struct NestedUnion
                {
                    [Variant] public static partial NestedUnion Value(int value);
                    [Variant] public static partial NestedUnion Empty();
                }
            }
            """;

        var result = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: true);
        AssertValidEditorCompilation(result);

        var container = RequireType(result, "PrivateDrawerContainer");
        var union = container.GetTypeMembers("NestedUnion").Single();
        var drawer = FindDrawerForTarget(result, union);
        Assert.NotNull(drawer.ContainingType);
        Assert.Equal("PrivateDrawerContainer", drawer.ContainingType!.Name);
        AssertThinWrapper(drawer, RequireType(result, MetadataTypeName));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_PlayerCompilationExcludesSupportAndWrappers()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Drawer_Basic"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain(
            result.OutputCompilation.References,
            reference => reference.Display is not null &&
                Path.GetFileName(reference.Display)
                    .StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase));
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName(SupportTypeName));
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName(MetadataTypeName));
        Assert.Empty(FindDrawers(result));
    }

    [Theory]
    [MemberData(nameof(NoSupportInputs))]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_NoSupportForNoUnionOrRejectedInput(
        string source,
        bool includeUnityReferences,
        string? expectedDiagnostic)
    {
        var result = UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: includeUnityReferences,
            includeEditorReferences: includeUnityReferences);

        if (expectedDiagnostic is null)
        {
            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        }
        else
        {
            Assert.Contains(
                result.GeneratorDiagnostics,
                diagnostic => diagnostic.Id == expectedDiagnostic);
        }

        AssertNoSharedSupport(result);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_MixedValidAndRejectedInputKeepsValidSupportSurface()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct MixedValidDrawerUnion
            {
                [Variant] public static partial MixedValidDrawerUnion Value(int value);
                [Variant] public static partial MixedValidDrawerUnion Empty();
            }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct MixedZeroVariantDrawerUnion
            {
            }
            """;

        var result = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: true);

        Assert.Contains(
            result.GeneratorDiagnostics,
            diagnostic => diagnostic.Id == "SPIRE_DU003");
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Single(SupportTrees(result));
        var validUnion = RequireType(result, "MixedValidDrawerUnion");
        AssertThinWrapper(
            FindDrawerForTarget(result, validUnion),
            RequireType(result, MetadataTypeName));
        Assert.Single(FindDrawers(result));
    }

    public static IEnumerable<object[]> NoSupportInputs => new[]
    {
        new object[]
        {
            "public sealed class NoUnionInput { public int value; }",
            true,
            null!,
        },
        new object[]
        {
            UnityGeneratorTestHarness.LoadFixture("NormalAdditive"),
            true,
            null!,
        },
        new object[]
        {
            """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityZeroVariantInput
            {
            }
            """,
            true,
            "SPIRE_DU003",
        },
        new object[]
        {
            """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public readonly partial struct UnityReadonlyRejectedInput
            {
                [Variant]
                public static partial UnityReadonlyRejectedInput Value(int value);
            }
            """,
            true,
            "SPIRE_DU013",
        },
        new object[]
        {
            """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial record UnityRecordRejectedInput
            {
                [Variant]
                public static partial UnityRecordRejectedInput Value(int value);
            }
            """,
            true,
            "SPIRE_DU013",
        },
    };

    [Fact]
    public void EditorDrawer_NoSupportWithoutUnityReferences_RemainsCoreOnly()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct UnityMissingEngineInput
            {
                [Variant] public static partial UnityMissingEngineInput Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: false,
            includeEditorReferences: false);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoSharedSupport(result);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorDrawer_CrossAssemblyInternalHelperIsolation()
    {
        var assemblyA = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;

            namespace DrawerAssemblyA
            {
                [Serializable]
                [DiscriminatedUnion]
                public partial struct AUnion
                {
                    [Variant] public static partial AUnion Value(int value);
                    [Variant] public static partial AUnion Empty();
                }
            }
            """,
            includeEditorReferences: true);
        AssertValidEditorCompilation(assemblyA);
        var assemblyAReference = EmitMetadataReference(assemblyA.OutputCompilation);

        var assemblyB = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;
            using DrawerAssemblyA;

            namespace DrawerAssemblyB
            {
                [Serializable]
                [DiscriminatedUnion]
                public partial struct BUnion
                {
                    [Variant] public static partial BUnion Text(string value);
                    [Variant] public static partial BUnion Empty();
                }

                public sealed class UsesExternalUnion
                {
                    public AUnion value;
                }
            }
            """,
            includeEditorReferences: true,
            additionalReferences: new[] { assemblyAReference });
        AssertValidEditorCompilation(assemblyB);

        var support = RequireType(assemblyB, SupportTypeName);
        var union = RequireType(assemblyB, "DrawerAssemblyB.BUnion");
        var drawer = FindDrawerForTarget(assemblyB, union);
        Assert.True(SymbolEqualityComparer.Default.Equals(support, drawer.BaseType));
        Assert.Single(SupportTrees(assemblyB));
        Assert.Single(FindDrawers(assemblyB));
    }

    private static void AssertValidEditorCompilation(UnityGeneratorRun result)
    {
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        UnityGeneratorTestHarness.AssertUnityAssembliesAvailable(includeEditor: true);
    }

    private static void AssertNoSharedSupport(UnityGeneratorRun result)
    {
        Assert.Empty(SupportTrees(result));
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName(SupportTypeName));
        Assert.Null(result.OutputCompilation.GetTypeByMetadataName(MetadataTypeName));
        Assert.Empty(FindDrawers(result));
    }

    private static IReadOnlyList<SyntaxTree> SupportTrees(UnityGeneratorRun result)
        => result.RunResult.GeneratedTrees
            .Where(tree => string.Equals(
                Path.GetFileName(tree.FilePath),
                SupportHint,
                StringComparison.Ordinal))
            .ToArray();

    private static INamedTypeSymbol RequireType(
        UnityGeneratorRun result,
        string metadataName)
    {
        var type = result.OutputCompilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(type);
        return type!;
    }

    private static IEnumerable<INamedTypeSymbol> FindDrawers(UnityGeneratorRun result)
        => GetAllTypes(result.OutputCompilation.GlobalNamespace)
            .Where(type => SymbolEqualityComparer.Default.Equals(
                type.ContainingAssembly,
                result.OutputCompilation.Assembly))
            .Where(type => type.GetAttributes().Any(IsCustomPropertyDrawer));

    private static INamedTypeSymbol FindDrawerForTarget(
        UnityGeneratorRun result,
        INamedTypeSymbol target,
        bool requireUnboundGenericTarget = false)
    {
        var matching = FindDrawers(result)
            .Where(drawer => drawer.GetAttributes().Any(attribute =>
                IsCustomPropertyDrawer(attribute) &&
                DrawerTargetMatches(attribute, target, requireUnboundGenericTarget)))
            .ToArray();
        Assert.Single(matching);
        return matching[0];
    }

    private static bool IsCustomPropertyDrawer(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == "UnityEditor.CustomPropertyDrawer";

    private static bool DrawerTargetMatches(
        AttributeData attribute,
        INamedTypeSymbol target,
        bool requireUnboundGenericTarget)
    {
        var argument = attribute.ConstructorArguments
            .FirstOrDefault(value => value.Kind == TypedConstantKind.Type);
        if (argument.Kind != TypedConstantKind.Type ||
            argument.Value is not INamedTypeSymbol drawerTarget)
        {
            return false;
        }

        if (requireUnboundGenericTarget && !drawerTarget.IsUnboundGenericType)
            return false;

        var expected = target.OriginalDefinition;
        var actual = drawerTarget.IsUnboundGenericType
            ? drawerTarget.OriginalDefinition
            : drawerTarget;
        return SymbolEqualityComparer.Default.Equals(actual, expected);
    }

    private static void AssertThinWrapper(
        INamedTypeSymbol drawer,
        INamedTypeSymbol metadataType)
    {
        Assert.Equal(
            "global::" + SupportTypeName,
            drawer.BaseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        Assert.False(drawer.IsGenericType);
        Assert.True(drawer.GetAttributes().Any(IsCustomPropertyDrawer));

        var metadata = MetadataField(drawer);
        Assert.True(metadata.IsStatic);
        Assert.True(metadata.IsReadOnly);
        Assert.True(SymbolEqualityComparer.Default.Equals(metadataType, metadata.Type));

        var constructors = drawer.InstanceConstructors
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .ToArray();
        Assert.Single(constructors);
        Assert.Equal(Accessibility.Public, constructors[0].DeclaredAccessibility);
        Assert.Empty(constructors[0].Parameters);
        var constructorSyntax = Assert.IsType<ConstructorDeclarationSyntax>(
            constructors[0].DeclaringSyntaxReferences.Single().GetSyntax());
        Assert.NotNull(constructorSyntax.Initializer);
        Assert.Equal(SyntaxKind.BaseConstructorInitializer, constructorSyntax.Initializer!.Kind());
        Assert.Equal("Metadata", Assert.Single(constructorSyntax.Initializer.ArgumentList.Arguments).Expression.ToString());

        var methods = drawer.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method =>
                !method.IsImplicitlyDeclared &&
                method.MethodKind == MethodKind.Ordinary)
            .ToArray();
        Assert.Empty(methods);
        Assert.Empty(drawer.GetTypeMembers().Where(type => !type.IsImplicitlyDeclared));
        Assert.DoesNotContain(
            drawer.BaseType!.GetAttributes(),
            IsCustomPropertyDrawer);
        AssertInheritedEntryPoints(drawer);
    }

    private static IFieldSymbol MetadataField(INamedTypeSymbol drawer)
    {
        var metadata = drawer.GetMembers("Metadata")
            .OfType<IFieldSymbol>()
            .Single();
        return metadata;
    }

    private static void AssertMetadataShape(
        INamedTypeSymbol metadata,
        INamedTypeSymbol variantMetadata)
    {
        Assert.Equal(Accessibility.Internal, metadata.DeclaredAccessibility);
        Assert.True(metadata.IsSealed);
        Assert.False(metadata.IsGenericType);

        AssertReadOnlyMember(metadata, "DeclaredUnionType");
        AssertReadOnlyMember(metadata, "Variants");
        Assert.DoesNotContain(
            new[] { "KindNames", "PayloadTypeNames", "VariantFieldNames" },
            name => metadata.GetMembers(name).Length != 0);
        Assert.Equal(
            2,
            metadata.InstanceConstructors
                .Where(constructor => !constructor.IsImplicitlyDeclared)
                .Single()
                .Parameters
                .Length);

        AssertSystemType(MemberType(metadata, "DeclaredUnionType"));
        AssertReadOnlyListOfVariantMetadata(
            MemberType(metadata, "Variants"),
            variantMetadata);

        Assert.Equal(Accessibility.Internal, variantMetadata.DeclaredAccessibility);
        Assert.True(variantMetadata.IsReadOnly);
        Assert.Equal(
            4,
            variantMetadata.InstanceConstructors
                .Where(constructor => !constructor.IsImplicitlyDeclared)
                .Single()
                .Parameters
                .Length);
        AssertReadOnlyMember(variantMetadata, "Ident");
        AssertReadOnlyMember(variantMetadata, "PayloadType");
        AssertReadOnlyMember(variantMetadata, "PayloadTypeAlias");
        AssertReadOnlyMember(variantMetadata, "Fields");
        Assert.Equal(SpecialType.System_String, MemberType(variantMetadata, "Ident").SpecialType);
        Assert.Equal(SpecialType.System_String, MemberType(variantMetadata, "PayloadType").SpecialType);
        Assert.Equal(SpecialType.System_String, MemberType(variantMetadata, "PayloadTypeAlias").SpecialType);
        AssertReadOnlyListOfString(MemberType(variantMetadata, "Fields"));
    }

    private static void AssertReadOnlyMember(INamedTypeSymbol type, string name)
    {
        var member = type.GetMembers(name).Single();
        switch (member)
        {
            case IFieldSymbol field:
                Assert.True(field.IsReadOnly, $"Metadata field '{name}' is mutable.");
                break;
            case IPropertySymbol property:
                Assert.Null(property.SetMethod);
                break;
            default:
                Assert.Fail($"Metadata member '{name}' was not a field or property.");
                break;
        }
    }

    private static ITypeSymbol MemberType(INamedTypeSymbol type, string name)
    {
        var member = type.GetMembers(name).Single();
        return member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => throw new Xunit.Sdk.XunitException(
                $"Metadata member '{name}' was not a typed field or property."),
        };
    }

    private static void AssertSystemType(ITypeSymbol type)
    {
        var named = Assert.IsAssignableFrom<INamedTypeSymbol>(type);
        Assert.Equal("System", named.ContainingNamespace.ToDisplayString());
        Assert.Equal("Type", named.Name);
    }

    private static void AssertReadOnlyListOfString(ITypeSymbol type)
    {
        var list = AssertReadOnlyList(type);
        var argument = Assert.Single(list.TypeArguments);
        Assert.Equal(SpecialType.System_String, argument.SpecialType);
    }

    private static void AssertReadOnlyListOfVariantMetadata(
        ITypeSymbol type,
        INamedTypeSymbol variantMetadata)
    {
        var list = AssertReadOnlyList(type);
        Assert.True(
            SymbolEqualityComparer.Default.Equals(
                variantMetadata,
                Assert.Single(list.TypeArguments)));
    }

    private static INamedTypeSymbol AssertReadOnlyList(ITypeSymbol type)
    {
        var list = Assert.IsAssignableFrom<INamedTypeSymbol>(type);
        Assert.Equal("System.Collections.Generic", list.ContainingNamespace.ToDisplayString());
        Assert.Equal("IReadOnlyList", list.Name);
        return list;
    }

    private static void AssertInheritedEntryPoints(INamedTypeSymbol drawer)
    {
        foreach (var (name, parameterCount) in new[]
        {
            ("CreatePropertyGUI", 1),
            ("OnGUI", 3),
            ("GetPropertyHeight", 2),
        })
        {
            var current = drawer.BaseType;
            IMethodSymbol? method = null;
            while (current is not null && method is null)
            {
                method = current.GetMembers(name)
                    .OfType<IMethodSymbol>()
                    .SingleOrDefault(candidate =>
                        !candidate.IsImplicitlyDeclared &&
                        candidate.DeclaredAccessibility == Accessibility.Public &&
                        candidate.Parameters.Length == parameterCount &&
                        (candidate.IsVirtual || candidate.IsOverride));
                current = current.BaseType;
            }

            Assert.NotNull(method);
            Assert.False(
                SymbolEqualityComparer.Default.Equals(method!.ContainingType, drawer),
                $"'{name}' should be inherited from the shared drawer base.");
        }
    }

    private static void AssertMetadataDeclaredUnionTypeIsNull(
        UnityGeneratorRun result,
        INamedTypeSymbol drawer)
    {
        var creation = MetadataCreation(result, drawer);
        var declaredUnionType = UnwrapOperation(creation.Arguments[0].Value);
        Assert.True(declaredUnionType.ConstantValue.HasValue);
        Assert.Null(declaredUnionType.ConstantValue.Value);
    }

    private static void AssertMetadataDeclaredUnionTypeMatches(
        UnityGeneratorRun result,
        INamedTypeSymbol drawer,
        INamedTypeSymbol union)
    {
        var creation = MetadataCreation(result, drawer);
        var declaredUnionType = Assert.IsAssignableFrom<ITypeOfOperation>(
            UnwrapOperation(creation.Arguments[0].Value));
        Assert.True(
            SymbolEqualityComparer.Default.Equals(declaredUnionType.TypeOperand, union),
            $"Metadata for '{union.ToDisplayString()}' points at '{declaredUnionType.TypeOperand}'.");
    }

    private static IReadOnlyList<(
        string Ident,
        string PayloadType,
        string? PayloadTypeAlias,
        IReadOnlyList<string> Fields)> ReadMetadataVariants(
        UnityGeneratorRun result,
        INamedTypeSymbol drawer)
    {
        var arguments = MetadataCreation(result, drawer).Arguments;
        Assert.Equal(2, arguments.Length);
        return ReadVariantRows(arguments[1].Value);
    }

    private static IObjectCreationOperation MetadataCreation(
        UnityGeneratorRun result,
        INamedTypeSymbol drawer)
    {
        var syntax = MetadataField(drawer)
            .DeclaringSyntaxReferences
            .Single()
            .GetSyntax();
        var semanticModel = result.OutputCompilation.GetSemanticModel(syntax.SyntaxTree);
        IObjectCreationOperation? creation = null;
        foreach (var expression in syntax.DescendantNodes().Where(node =>
                     node is ObjectCreationExpressionSyntax ||
                     node is ImplicitObjectCreationExpressionSyntax))
        {
            if (semanticModel.GetOperation(expression) is not IObjectCreationOperation candidate ||
                !SymbolEqualityComparer.Default.Equals(candidate.Type, MetadataField(drawer).Type))
            {
                continue;
            }

            Assert.Null(creation);
            creation = candidate;
        }

        Assert.NotNull(creation);
        return creation!;
    }

    private static IReadOnlyList<(
        string Ident,
        string PayloadType,
        string? PayloadTypeAlias,
        IReadOnlyList<string> Fields)> ReadVariantRows(IOperation operation)
    {
        var array = Assert.IsAssignableFrom<IArrayCreationOperation>(UnwrapOperation(operation));
        Assert.NotNull(array.Initializer);
        return array.Initializer!.ElementValues
            .Select(ReadVariantMetadata)
            .ToArray();
    }

    private static (
        string Ident,
        string PayloadType,
        string? PayloadTypeAlias,
        IReadOnlyList<string> Fields) ReadVariantMetadata(IOperation operation)
    {
        var creation = Assert.IsAssignableFrom<IObjectCreationOperation>(UnwrapOperation(operation));
        Assert.Equal(4, creation.Arguments.Length);
        return (
            ReadStringConstant(creation.Arguments[0].Value),
            ReadStringConstant(creation.Arguments[1].Value),
            ReadNullableStringConstant(creation.Arguments[2].Value),
            ReadStringArray(creation.Arguments[3].Value));
    }

    private static IReadOnlyList<string> ReadStringArray(IOperation operation)
    {
        operation = UnwrapOperation(operation);
        if (operation is IInvocationOperation invocation &&
            invocation.TargetMethod.Name == "Empty")
        {
            return Array.Empty<string>();
        }

        var array = Assert.IsAssignableFrom<IArrayCreationOperation>(operation);
        Assert.NotNull(array.Initializer);
        return array.Initializer!.ElementValues
            .Select(ReadStringConstant)
            .ToArray();
    }

    private static string ReadStringConstant(IOperation operation)
    {
        operation = UnwrapOperation(operation);
        Assert.True(operation.ConstantValue.HasValue);
        return Assert.IsType<string>(operation.ConstantValue.Value);
    }

    private static string? ReadNullableStringConstant(IOperation operation)
    {
        operation = UnwrapOperation(operation);
        Assert.True(operation.ConstantValue.HasValue);
        return operation.ConstantValue.Value as string;
    }

    private static IOperation UnwrapOperation(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    continue;
                default:
                    return operation;
            }
        }
    }

    private static void AssertVariantRowsEqual(
        IReadOnlyList<(
            string Ident,
            string PayloadType,
            string? PayloadTypeAlias,
            IReadOnlyList<string> Fields)> expected,
        IReadOnlyList<(
            string Ident,
            string PayloadType,
            string? PayloadTypeAlias,
            IReadOnlyList<string> Fields)> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var row = 0; row < expected.Count; row++)
        {
            Assert.Equal(expected[row].Ident, actual[row].Ident);
            Assert.Equal(expected[row].PayloadType, actual[row].PayloadType);
            Assert.Equal(expected[row].PayloadTypeAlias, actual[row].PayloadTypeAlias);
            Assert.Equal(expected[row].Fields, actual[row].Fields);
        }
    }

    internal static void AssertKindChoicesUseMetadata(
        UnityGeneratorRun result,
        string unionMetadataName,
        params string[] expectedKindNames)
    {
        var union = RequireType(result, unionMetadataName);
        var drawer = FindDrawerForTarget(result, union);
        var metadata = RequireType(result, MetadataTypeName);
        var variants = ReadMetadataVariants(result, drawer);
        var identifiers = variants.Select(variant => variant.Ident).ToArray();

        Assert.Equal(expectedKindNames, identifiers);
        Assert.DoesNotContain("Mixed", identifiers);
        Assert.DoesNotContain("Invalid", identifiers);

        var support = RequireType(result, SupportTypeName);
        var variantsReferences = support.DeclaringSyntaxReferences
            .SelectMany(reference =>
                reference.GetSyntax().DescendantNodes().OfType<IdentifierNameSyntax>())
            .Where(identifier => identifier.Identifier.ValueText == "Variants")
            .Select(identifier =>
            {
                var model = result.OutputCompilation.GetSemanticModel(identifier.SyntaxTree);
                return model.GetSymbolInfo(identifier).Symbol;
            })
            .Any(symbol => SymbolEqualityComparer.Default.Equals(
                symbol,
                metadata.GetMembers("Variants").Single()));
        Assert.True(
            variantsReferences,
            "The shared drawer support must read variant choices from metadata.Variants.");
    }

    private static MetadataReference EmitMetadataReference(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(
            emit.Success,
            "The first drawer assembly could not be emitted:\n" +
            string.Join("\n", emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNamespace))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var descendant in GetNestedTypes(nested))
                yield return descendant;
        }
    }
}
