using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

public sealed class UnitySerializationGeneratorTests
{
    public static IEnumerable<object[]> AcceptedFixtures => new[]
    {
        new object[] { "Serializable_BasicStruct" },
        new object[] { "Serializable_AliasAttribute" },
        new object[] { "Serializable_OnOtherPartial" },
        new object[] { "Serializable_VariantsOnOtherPartial" },
        new object[] { "Serializable_AutoLayout" },
        new object[] { "Serializable_ExplicitAdditive" },
        new object[] { "Serializable_GlobalOverride" },
        new object[] { "Serializable_ExplicitBoxedFields" },
        new object[] { "Serializable_ExplicitBoxedTuple" },
        new object[] { "Serializable_BoxedFields_GenericIntString" },
        new object[] { "Serializable_BoxedTuple_GenericIntString" },
        new object[] { "Serializable_BoxedFields_NestedContainingType" },
        new object[] { "Serializable_BoxedTuple_NestedContainingType" },
        new object[] { "Serializable_BoxedLayouts_CompatibleUserNames" },
        new object[] { "Payload_FieldMatrix" },
        new object[] { "Payload_Enums" },
        new object[] { "Payload_Native" },
        new object[] { "Payload_Serializable" },
        new object[] { "Payload_Collections" },
        new object[] { "Payload_FieldlessDuplicate" },
        new object[] { "Payload_Escaped" },
        new object[] { "Payload_Nested" },
        new object[] { "Payload_NestedCollections" },
        new object[] { "Generic_IntString" },
        new object[] { "Generic_ChildGraph" },
        new object[] { "Generic_Open" },
        new object[] { "Generic_Containing" },
        new object[] { "NormalAdditive" },
        new object[] { "Normal_Generic" },
        new object[] { "Normal_HelperSerializable" },
        new object[] { "Drawer_Basic" },
    };

    public static IEnumerable<object[]> SerializableAcceptedFixtures =>
        AcceptedFixtures.Where(fixture =>
            fixture[0] is not "NormalAdditive" and
            not "Normal_Generic" and
            not "Normal_HelperSerializable");

    public static IEnumerable<object[]> NormalNonSerializableFixtures => new[]
    {
        new object[] { "NormalAdditive", 0 },
        new object[] { "Normal_Generic", 1 },
        new object[] { "Normal_HelperSerializable", 0 },
    };

    [Fact]
    public void RoslynHost_Uses410AndInstalledUnityAssemblies()
    {
        UnityGeneratorTestHarness.AssertUnityAssembliesAvailable(includeEditor: true);

        var version = typeof(CSharpCompilation).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.Equal(4, version!.Major);
        Assert.Equal(10, version.Minor);
        Assert.EndsWith(
            "UnityEngine.CoreModule.dll",
            System.IO.Path.Combine(
                UnityGeneratorTestHarness.UnityEngineRoot,
                "UnityEngine.CoreModule.dll"));
    }

    [Theory]
    [MemberData(nameof(AcceptedFixtures))]
    public void AcceptedSources_CompileAgainstRealUnityMetadata(string fixtureName)
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture(fixtureName));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        if (fixtureName is "NormalAdditive" or "Normal_Generic" or "Normal_HelperSerializable")
        {
            Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        }
        else
        {
            Assert.Contains("SerializeField", result.GeneratedSource);
        }
    }

    [Theory]
    [MemberData(nameof(SerializableAcceptedFixtures))]
    public void AcceptedSources_GeneratedTreesHaveNoNullableWarnings(string fixtureName)
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture(fixtureName));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var nullableWarnings = GeneratedNullableWarnings(result);

        Assert.True(
            nullableWarnings.Length == 0,
            "Generated Unity source emitted nullable warnings for fixture '" + fixtureName + "':\n" +
            string.Join(
                "\n",
                nullableWarnings.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
    }

    [Theory]
    [MemberData(nameof(SerializableAcceptedFixtures))]
    public void AcceptedSources_CSharp9GeneratedTreesHaveNoNullableWarnings(string fixtureName)
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture(fixtureName),
            languageVersion: LanguageVersion.CSharp9);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var nullableWarnings = GeneratedNullableWarnings(result);
        Assert.True(
            nullableWarnings.Length == 0,
            "Generated Unity source emitted nullable warnings under the Unity C# 9 profile for fixture '" +
            fixtureName + "':\n" +
            string.Join(
                "\n",
                nullableWarnings.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
    }

    [Theory]
    [MemberData(nameof(NormalNonSerializableFixtures))]
    public void NormalNonSerializableFixtures_KeepNullableWarningBaseline(string fixtureName, int expectedWarningCount)
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture(fixtureName));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var warnings = GeneratedNullableWarnings(result);
        Assert.Equal(expectedWarningCount, warnings.Length);
        if (fixtureName == "Normal_Generic")
            Assert.All(warnings, warning => Assert.Equal("CS8604", warning.Id));
    }

    [Fact]
    public void SerializableOptIn_IsSemanticAcrossAliasesAndPartials()
    {
        foreach (var (fixtureName, metadataName) in new[]
        {
            ("Serializable_BasicStruct", "UnityBasic"),
            ("Serializable_AliasAttribute", "UnityAlias"),
            ("Serializable_OnOtherPartial", "UnityPartialAttribute"),
            ("Serializable_VariantsOnOtherPartial", "UnityPartialVariants"),
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName));

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

            var union = FindUnion(result, metadataName);
            Assert.NotNull(FindField(union, "_kind"));
            Assert.NotNull(FindField(union, "_serializedPayload"));
            Assert.Contains(
                union.AllInterfaces,
                interfaceType => interfaceType.ToDisplayString() ==
                    "UnityEngine.ISerializationCallbackReceiver");
        }
    }

    [Fact]
    public void SerializableLayouts_ResolveToAdditiveIncludingGlobalOverride()
    {
        foreach (var fixtureName in new[]
        {
            "Serializable_BasicStruct",
            "Serializable_AutoLayout",
            "Serializable_ExplicitAdditive",
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName));

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
            Assert.DoesNotContain("FieldOffset", result.GeneratedSource);
        }

        var globalOverride = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_GlobalOverride"),
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultLayout"] = "Overlap",
            });

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(globalOverride);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(globalOverride);
        Assert.DoesNotContain("FieldOffset", globalOverride.GeneratedSource);
    }

    [Fact]
    public void NormalAdditive_LeavesExistingRuntimeSurfaceUnchanged()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("NormalAdditive"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityNormalAdditive");
        Assert.NotNull(union.GetMembers("kind").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("Value").OfType<IMethodSymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("Deconstruct").OfType<IMethodSymbol>().SingleOrDefault());
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        Assert.DoesNotContain("SerializeReference", result.GeneratedSource);
        Assert.DoesNotContain("CustomPropertyDrawer", result.GeneratedSource);

        var helperOnly = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Normal_HelperSerializable"));
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(helperOnly);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(helperOnly);
        Assert.DoesNotContain("ISerializationCallbackReceiver", helperOnly.GeneratedSource);
        Assert.DoesNotContain("SerializeField", helperOnly.GeneratedSource);
    }

    [Fact]
    public void RuntimeAndSerializedFields_HaveTheDeclaredUnitySchema()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_BasicStruct"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityBasic");
        var kindField = FindField(union, "_kind");
        var payloadField = FindField(union, "_serializedPayload");

        Assert.NotNull(kindField);
        Assert.NotNull(payloadField);
        Assert.False(kindField!.IsReadOnly);
        Assert.False(kindField.IsStatic);
        Assert.Equal(TypeKind.Enum, kindField.Type.TypeKind);
        Assert.Equal("Kind", kindField.Type.Name);
        Assert.True(HasAttribute(kindField, "System.NonSerializedAttribute"));
        Assert.False(HasAttribute(kindField, "UnityEngine.SerializeField"));
        Assert.True(HasAttribute(payloadField!, "UnityEngine.SerializeReference"));

        var serializedFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                HasAttribute(field, "UnityEngine.SerializeField"))
            .ToArray();
        var managedReferenceFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                HasAttribute(field, "UnityEngine.SerializeReference"))
            .ToArray();
        Assert.Empty(serializedFields);
        Assert.Single(managedReferenceFields);
        Assert.Equal("_serializedPayload", managedReferenceFields[0].Name);

        var runtimeFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic && field.Name != "_kind" &&
                field.Name != "_serializedPayload")
            .ToArray();

        Assert.NotEmpty(runtimeFields);
        Assert.All(runtimeFields, field =>
            Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                $"Runtime field '{field.Name}' must be explicitly non-serialized."));

        var payloadTypes = union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .ToArray();

        Assert.True(
            payloadTypes.Length >= 4,
            "Expected a serializable base payload and one typed payload per variant.");
        Assert.Contains(
            payloadTypes,
            type => !type.GetMembers().OfType<IFieldSymbol>()
                .Any(field => !field.IsStatic));
    }

    [Fact]
    public void PayloadFieldMatrix_PreservesTypedNativeAndManagedFields()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Payload_FieldMatrix"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityPayloadFieldMatrix");
        var payloadTypes = union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"));
        var payloadFields = payloadTypes
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .ToArray();

        Assert.Contains(payloadFields, field =>
            field.Type.SpecialType == SpecialType.System_Boolean);
        Assert.Contains(payloadFields, field =>
            field.Type.SpecialType == SpecialType.System_Int32);
        Assert.Contains(payloadFields, field =>
            field.Type.SpecialType == SpecialType.System_Single);
        Assert.Contains(payloadFields, field =>
            field.Type.SpecialType == SpecialType.System_Double);
        Assert.Contains(payloadFields, field =>
            field.Type.SpecialType == SpecialType.System_String);
        Assert.Contains(payloadFields, field =>
            field.Type is INamedTypeSymbol named &&
            named.TypeKind == TypeKind.Enum &&
            named.Name == "SmallEnum");
        Assert.Contains(payloadFields, field =>
            IsNamedType(field.Type, "UnityEngine.Vector3"));
        Assert.Contains(payloadFields, field =>
            IsNamedType(field.Type, "UnityEngine.Color"));
        Assert.Contains(payloadFields, field =>
            IsNamedType(field.Type, "UnityEngine.Texture2D"));
        Assert.Contains(payloadFields, field =>
            IsNamedType(field.Type, "SerializableData"));
        Assert.Contains(payloadFields, field =>
            field.Type is IArrayTypeSymbol array &&
            array.Rank == 1 &&
            array.ElementType.SpecialType == SpecialType.System_Int32);
        Assert.Contains(payloadFields, field =>
            field.Type is INamedTypeSymbol list &&
            list.Name == "List" &&
            list.TypeArguments.Length == 1 &&
            IsNamedType(list.TypeArguments[0], "SerializableData"));
    }

    [Fact]
    public void GeneratedPayloadFields_CarrySerializeFieldAcrossAllLayouts()
    {
        foreach (var (fixtureName, metadataName) in new[]
        {
            ("Serializable_ExplicitAdditive", "UnityExplicitAdditive"),
            ("Serializable_ExplicitBoxedFields", "UnityBoxedFieldsUnion"),
            ("Serializable_ExplicitBoxedTuple", "UnityBoxedTupleUnion"),
            ("Serializable_ExplicitUnsafeOverlap", "UnityUnsafeOverlap"),
            ("Serializable_ExplicitOverlap", "UnityOverlapUnion"),
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName));

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

            var union = FindUnion(result, metadataName);
            var payloadTypes = union.GetTypeMembers()
                .Where(type => HasAttribute(type, "System.SerializableAttribute"))
                .ToArray();
            var concretePayloadTypes = payloadTypes
                .Where(type => !type.IsAbstract)
                .ToArray();
            var payloadFields = concretePayloadTypes
                .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
                .Where(field => !field.IsStatic)
                .ToArray();

            Assert.Contains(
                concretePayloadTypes,
                type => !type.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic));
            Assert.NotEmpty(payloadFields);
            Assert.All(payloadFields, field =>
            {
                Assert.Equal(Accessibility.Public, field.DeclaredAccessibility);
                Assert.True(
                    HasAttribute(field, "UnityEngine.SerializeField"),
                    $"Payload field '{field.ContainingType.Name}.{field.Name}' in " +
                    $"{fixtureName} must carry UnityEngine.SerializeField.");
            });
        }
    }

    [Fact]
    public void CanonicalPayloadSchema_AllFiveLayouts()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_AllLayouts"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var metadataName in new[]
        {
            "StabilityAdditiveUnion",
            "StabilityBoxedFieldsUnion",
            "StabilityBoxedTupleUnion",
            "StabilityUnsafeOverlapUnion",
            "StabilityOverlapUnion",
        })
        {
            AssertPayloadFirstSchema(FindUnion(result, metadataName));
        }
    }

    [Fact]
    public void FormerNamePayloadClass_FieldParity()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_Aliases"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var migration = FindUnion(result, "StabilityMigrationUnion");
        AssertPayloadFirstSchema(migration);

        var payloadTypes = SerializablePayloadTypes(migration)
            .Where(type => !type.IsAbstract)
            .ToArray();
        Assert.Equal(5, payloadTypes.Length);

        var canonical = payloadTypes.Single(type => type.Name.Contains("Renamed", StringComparison.Ordinal) &&
            !type.Name.Contains("OldRenamed", StringComparison.Ordinal));
        var formerNamePayload = payloadTypes.Single(type =>
            type.Name.Contains("OldRenamed", StringComparison.Ordinal));
        Assert.Equal(Accessibility.Private, formerNamePayload.DeclaredAccessibility);
        Assert.Equal(
            PayloadTypeFieldSignatures(canonical),
            PayloadTypeFieldSignatures(formerNamePayload));
        Assert.All(
            InstancePayloadFields(formerNamePayload),
            field => Assert.True(
                HasAttribute(field, "UnityEngine.SerializeField"),
                $"Former-name payload field '{formerNamePayload.Name}.{field.Name}' must carry SerializeField."));

        var canonicalEmpty = payloadTypes.Single(type =>
            type.Name.Contains("Empty", StringComparison.Ordinal) &&
            !type.Name.Contains("OldEmpty", StringComparison.Ordinal));
        var formerNameEmptyPayload = payloadTypes.Single(type =>
            type.Name.Contains("OldEmpty", StringComparison.Ordinal));
        Assert.Equal(Accessibility.Private, formerNameEmptyPayload.DeclaredAccessibility);
        Assert.Empty(InstancePayloadFields(canonicalEmpty));
        Assert.Empty(InstancePayloadFields(formerNameEmptyPayload));

        var generic = FindUnion(result, "StabilityGenericUnion`1");
        var genericFormerNamePayload = SerializablePayloadTypes(generic)
            .Single(type => type.Name.Contains("OldFirst", StringComparison.Ordinal));
        Assert.Equal(Accessibility.Private, genericFormerNamePayload.DeclaredAccessibility);
        var genericField = Assert.Single(InstancePayloadFields(genericFormerNamePayload));
        Assert.Equal(TypeKind.TypeParameter, genericField.Type.TypeKind);
        Assert.True(HasAttribute(genericField, "UnityEngine.SerializeField"));
    }

    [Fact]
    public void FormerlySerializedAs_AllFiveLayouts()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_Aliases"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        foreach (var metadataName in new[]
        {
            "StabilityAliasAdditive",
            "StabilityAliasBoxedFields",
            "StabilityAliasBoxedTuple",
            "StabilityAliasUnsafeOverlap",
            "StabilityAliasOverlap",
        })
        {
            var union = FindUnion(result, metadataName);
            AssertPayloadFirstSchema(union);
            var formerNamePayload = SerializablePayloadTypes(union)
                .Where(type => !type.IsAbstract)
                .Single(type => type.Name.Contains("OldFirst", StringComparison.Ordinal));
            Assert.Equal(Accessibility.Private, formerNamePayload.DeclaredAccessibility);
            var field = Assert.Single(InstancePayloadFields(formerNamePayload));
            Assert.Equal("value", field.Name);
            Assert.Equal(SpecialType.System_Int32, field.Type.SpecialType);
            Assert.True(HasAttribute(field, "UnityEngine.SerializeField"));
        }
    }

    [Fact]
    public void FormerlySerializedAs_KeywordAliasIsValid()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_Aliases"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "StabilityKeywordAliasUnion");
        AssertPayloadFirstSchema(union);
        var concretePayloadTypes = SerializablePayloadTypes(union)
            .Where(type => !type.IsAbstract)
            .ToArray();
        Assert.Equal(3, concretePayloadTypes.Length);
        var fieldedPayloads = concretePayloadTypes
            .Where(type => InstancePayloadFields(type).Length == 1)
            .ToArray();
        Assert.Equal(2, fieldedPayloads.Length);
        Assert.All(
            fieldedPayloads.SelectMany(InstancePayloadFields),
            field => Assert.True(HasAttribute(field, "UnityEngine.SerializeField")));
    }

    [Fact]
    public void FormerlySerializedAs_NullEmptyAndSelfNoOp()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilityAliasNoOps
            {
                [Variant(FormerlySerializedAs = null)]
                public static partial StabilityAliasNoOps First();

                [Variant(FormerlySerializedAs = "")]
                public static partial StabilityAliasNoOps Empty();

                [Variant(FormerlySerializedAs = "Current")]
                public static partial StabilityAliasNoOps Current();

                [Variant]
                public static partial StabilityAliasNoOps Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "StabilityAliasNoOps");
        AssertPayloadFirstSchema(union);
        Assert.Equal(
            4,
            SerializablePayloadTypes(union).Count(type => !type.IsAbstract));
    }

    [Fact]
    public void NonUnityAlias_DoesNotChangeGeneration()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct StabilityNonUnityAlias
            {
                [Variant(FormerlySerializedAs = "OldValue")]
                public static partial StabilityNonUnityAlias Value(int value);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ISerializationCallbackReceiver", result.GeneratedSource);
        Assert.DoesNotContain("SerializeField", result.GeneratedSource);
        Assert.DoesNotContain("SerializeReference", result.GeneratedSource);
        Assert.DoesNotContain("OldValue", result.GeneratedSource);
    }

    [Fact]
    public void Drawer_PayloadTypeSelectorWithoutKindProperty()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Stability_AllLayouts"),
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "StabilityAdditiveUnion");
        var drawer = FindDrawerForTarget(result, union);
        AssertDrawerEntryPoints(drawer);

        var drawerSource = Assert.Single(drawer.DeclaringSyntaxReferences)
            .GetSyntax()
            .ToFullString();
        Assert.DoesNotContain("_kind", drawerSource);
        Assert.Contains("managedReferenceValue", drawerSource);
    }

    [Fact]
    public void Drawer_PayloadTypeDispatchUsesExactTypeIdentity()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilityDrawerPayloadNameCollision
            {
                [Variant(FormerlySerializedAs = "OldPayload")]
                public static partial StabilityDrawerPayloadNameCollision Current(int value);

                [Variant]
                public static partial StabilityDrawerPayloadNameCollision Old(string label);
            }
            """;

        var result = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "StabilityDrawerPayloadNameCollision");
        var drawer = FindDrawerForTarget(result, union);
        AssertDrawerEntryPoints(drawer);
        var drawerSource = Assert.Single(drawer.DeclaringSyntaxReferences)
            .GetSyntax()
            .ToFullString();

        Assert.Contains("__SpireOldPayload", drawerSource);
        Assert.Contains("__SpireOldPayloadPayload", drawerSource);
        Assert.Contains("managedReferenceValue", drawerSource);

        var payloadDispatchMethod = drawer.DeclaringSyntaxReferences
            .Single()
            .GetSyntax()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.Text == "IsPayloadTypeName");
        var payloadDispatchSource = payloadDispatchMethod.ToFullString();
        Assert.Contains("LastIndexOf", payloadDispatchSource);
        Assert.Contains("typeName[end]", payloadDispatchSource);
        Assert.DoesNotContain("Contains(", payloadDispatchSource);
    }

    [Fact]
    public void IncrementalGeneration_UnchangedUnionOutputIsCachedWhenUnrelatedSourceChanges()
    {
        const string unionSource = """
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct StabilityTrackedUnion
            {
                [Variant] public static partial StabilityTrackedUnion Value(int amount);
                [Variant] public static partial StabilityTrackedUnion Empty();
            }
            """;
        const string unrelatedSource = "public static class StabilityUnrelated { public const int Value = 1; }";
        const string changedUnrelatedSource = "public static class StabilityUnrelated { public const int Value = 2; }";

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview);
        var unionTree = CSharpSyntaxTree.ParseText(
            unionSource,
            parseOptions,
            path: "stability-union.cs");
        var unrelatedTree = CSharpSyntaxTree.ParseText(
            unrelatedSource,
            parseOptions,
            path: "stability-unrelated.cs");
        var firstCompilation = CSharpCompilation.Create(
            "StabilityIncrementalAssembly",
            new[] { unionTree, unrelatedTree },
            UnityGeneratorTestHarness.GetReferences(
                includeUnityReferences: true,
                includeEditorReferences: false),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        var generator = new DiscriminatedUnionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            firstCompilation,
            out _,
            out var firstDiagnostics);
        Assert.Empty(firstDiagnostics);
        var firstRun = driver.GetRunResult();
        var firstSource = firstRun.GeneratedTrees
            .Single(tree =>
                Path.GetFileName(tree.FilePath)
                    .Contains("StabilityTrackedUnion", StringComparison.Ordinal) &&
                !Path.GetFileName(tree.FilePath)
                    .Contains(".UnityDrawer", StringComparison.Ordinal))
            .GetText()
            .ToString();

        var changedUnrelatedTree = CSharpSyntaxTree.ParseText(
            changedUnrelatedSource,
            parseOptions,
            path: "stability-unrelated.cs");
        var secondCompilation = firstCompilation.ReplaceSyntaxTree(
            unrelatedTree,
            changedUnrelatedTree);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            secondCompilation,
            out _,
            out var secondDiagnostics);
        Assert.Empty(secondDiagnostics);
        var secondRun = driver.GetRunResult();
        var secondSource = secondRun.GeneratedTrees
            .Single(tree =>
                Path.GetFileName(tree.FilePath)
                    .Contains("StabilityTrackedUnion", StringComparison.Ordinal) &&
                !Path.GetFileName(tree.FilePath)
                    .Contains(".UnityDrawer", StringComparison.Ordinal))
            .GetText()
            .ToString();
        Assert.Equal(firstSource, secondSource);

        var trackedOutputs = secondRun.Results
            .SelectMany(result => result.TrackedSteps.Values)
            .SelectMany(steps => steps)
            .SelectMany(step => step.Outputs)
            .ToArray();
        Assert.Contains(
            trackedOutputs,
            output => output.Reason == IncrementalStepRunReason.Cached &&
                output.Value?.ToString()?.Contains(
                    "StabilityTrackedUnion",
                    StringComparison.Ordinal) == true);
    }

    [Fact]
    public void SupportedFieldShapes_IncludeEnumsNativeTypesSerializableTypesCollectionsAndEscapes()
    {
        foreach (var fixtureName in new[]
        {
            "Payload_Enums",
            "Payload_Native",
            "Payload_Serializable",
            "Payload_Collections",
            "Payload_FieldlessDuplicate",
            "Payload_Escaped",
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName));

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        }
    }

    [Fact]
    public void NestedUnion_CompilesAndEmitsCallbackGraphSurface()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Payload_Nested"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        Assert.NotNull(FindUnion(result, "UnityNestedChild"));
        Assert.NotNull(FindUnion(result, "UnityNestedParent"));
        Assert.Contains("OnBeforeSerialize", result.GeneratedSource);
        Assert.Contains("OnAfterDeserialize", result.GeneratedSource);
        Assert.Contains("_serializedPayload", result.GeneratedSource);
    }

    [Fact]
    public void NestedCollectionUnion_CompilesArrayAndListPayloadPaths()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Payload_NestedCollections"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("OnBeforeSerialize", result.GeneratedSource);
        Assert.Contains("OnAfterDeserialize", result.GeneratedSource);
        Assert.Contains("array", result.GeneratedSource);
        Assert.Contains("list", result.GeneratedSource);
    }

    [Fact]
    public void GenericClosedIntAndString_CompileAgainstInstalledUnityMetadata()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Generic_IntString"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityGeneric`1");
        Assert.Contains(
            union.GetMembers("Some").OfType<IMethodSymbol>(),
            method => method.IsStatic);
        Assert.Contains(
            union.GetMembers("None").OfType<IMethodSymbol>(),
            method => method.IsStatic);
        Assert.Contains("UnityGeneric<T>", result.GeneratedSource);
        Assert.Contains("UnityGenericUses", result.OutputCompilation.SyntaxTrees
            .Single(tree => tree.FilePath == "unity-case.cs")
            .GetText()
            .ToString());
    }

    [Fact]
    public void GenericContainingNestedUnion_DrawerCompilesAgainstEditorMetadata()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Generic_Containing"),
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("CustomPropertyDrawer", result.GeneratedSource);
    }

    [Fact]
    public void GenericOpenTypeParameter_RemainsAValidCompilationProbe()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Generic_Open"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.NotNull(FindUnion(result, "UnityGenericOpen`1"));
    }

    [Fact]
    public void GeneratedSourceSnapshots_BasicAndGenericContainStableContractSurface()
    {
        var basic = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_BasicStruct"));
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(basic);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(basic);
        Assert.Contains("public enum Kind", basic.GeneratedSource);
        Assert.Contains("SerializeField", basic.GeneratedSource);
        Assert.Contains("SerializeReference", basic.GeneratedSource);
        Assert.Contains("ISerializationCallbackReceiver", basic.GeneratedSource);
        Assert.Contains("OnBeforeSerialize", basic.GeneratedSource);
        Assert.Contains("OnAfterDeserialize", basic.GeneratedSource);

        var generic = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Generic_IntString"));
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(generic);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(generic);
        Assert.Contains("UnityGeneric<T>", generic.GeneratedSource);
        Assert.Contains("Some", generic.GeneratedSource);
        Assert.Contains("None", generic.GeneratedSource);
    }

    [Fact]
    public void GeneratedInitAndCallbacks_ExposeMutationWithoutCacheInvalidation()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_BasicStruct"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityBasic");
        foreach (var fieldName in new[] { "radius", "text", "icon" })
        {
            var property = union.GetMembers(fieldName).OfType<IPropertySymbol>().Single();
            Assert.NotNull(property.GetMethod);
            Assert.NotNull(property.SetMethod);
            Assert.True(property.SetMethod!.IsInitOnly);
        }

        var runtimeSlots = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => field.Name.StartsWith("__s", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(runtimeSlots);
        Assert.All(runtimeSlots, field =>
        {
            Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                "Runtime storage field '" + field.Name + "' must not be serialized by Unity.");
        });

        Assert.DoesNotContain("__SpireDetachSerializedCache", result.GeneratedSource);

        Assert.NotNull(union.GetMembers("_serializedPayload")
            .OfType<IFieldSymbol>()
            .SingleOrDefault());
        Assert.Contains(
            union.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "OnBeforeSerialize" &&
                method.Parameters.Length == 0);
        Assert.Contains(
            union.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "OnAfterDeserialize" &&
                method.Parameters.Length == 0);
    }

    [Fact]
    public void GeneratedCallbacks_DefineKnownMismatchAndUnknownDefaultBranches()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_BasicStruct"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityBasic");
        var kind = union.GetTypeMembers("Kind").Single();
        Assert.Equal(3, kind.GetMembers().OfType<IFieldSymbol>().Count());
        Assert.Contains(
            union.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "OnAfterDeserialize" &&
                method.Parameters.Length == 0);
        Assert.NotNull(union.GetMembers("_serializedPayload")
            .OfType<IFieldSymbol>()
            .SingleOrDefault());
    }

    [Fact]
    public void GeneratedFactoriesAndInitProfile_DoNotEagerAllocatePerValue()
    {
        const string source = """
            using System;
            using Houtamelo.Spire;
            using UnityEngine;

            [Serializable]
            [DiscriminatedUnion(Layout.Additive)]
            public partial struct UnityAllocationProbe
            {
                [Variant]
                public static partial UnityAllocationProbe Text(int count, string label);

                [Variant]
                public static partial UnityAllocationProbe Empty();
            }

            public static class UnityAllocationProbeHarness
            {
                public static long MeasureFactory(int iterations)
                {
                    var warmupChecksum = 0;
                    for (var index = 0; index < 64; index++)
                    {
                        var warmup = UnityAllocationProbe.Text(index, "steady");
                        warmupChecksum += warmup.count;
                    }

                    GC.KeepAlive(warmupChecksum);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var checksum = 0;
                    for (var index = 0; index < iterations; index++)
                    {
                        var value = UnityAllocationProbe.Text(index, "steady");
                        checksum += value.count;
                    }

                    GC.KeepAlive(checksum);
                    return GC.GetAllocatedBytesForCurrentThread() - before;
                }

                public static long MeasureInit(int iterations)
                {
                    var warmupChecksum = 0;
                    for (var index = 0; index < 64; index++)
                    {
                        var warmup = new UnityAllocationProbe
                        {
                            count = index,
                            label = "steady",
                        };
                        warmupChecksum += warmup.count;
                    }

                    GC.KeepAlive(warmupChecksum);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var checksum = 0;
                    for (var index = 0; index < iterations; index++)
                    {
                        var value = new UnityAllocationProbe
                        {
                            count = index,
                            label = "steady",
                        };
                        checksum += value.count;
                    }

                    GC.KeepAlive(checksum);
                    return GC.GetAllocatedBytesForCurrentThread() - before;
                }
            }
            """;

        var result = UnityGeneratorTestHarness.Run(source);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = EmitAndLoad(result);
        const int iterations = 4096;
        var factoryBytes = InvokeAllocationProfile(assembly, "MeasureFactory", iterations);
        var initBytes = InvokeAllocationProfile(assembly, "MeasureInit", iterations);
        var maximumExpectedBytes = Math.Max(1024L, iterations * 2L);

        Assert.True(
            factoryBytes < maximumExpectedBytes,
            $"Factory profile allocated {factoryBytes} bytes for {iterations} values; eager per-value allocation remains.");
        Assert.True(
            initBytes < maximumExpectedBytes,
            $"Init profile allocated {initBytes} bytes for {iterations} values; eager per-value allocation remains.");
    }

    [Fact]
    public void EditorDrawer_IsGuardedAndCompilesAgainstRealUnityEditorMetadata()
    {
        var source = UnityGeneratorTestHarness.LoadFixture("Drawer_Basic");
        var runtime = UnityGeneratorTestHarness.Run(source);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(runtime);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(runtime);
        Assert.DoesNotContain(
            runtime.OutputCompilation.References,
            reference => reference.Display is not null &&
                System.IO.Path.GetFileName(reference.Display)
                    .StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("#if UNITY_EDITOR", runtime.GeneratedSource);

        var editor = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(editor);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(editor);
        Assert.Contains(
            editor.OutputCompilation.References,
            reference => reference.Display is not null &&
                reference.Display.EndsWith("UnityEditor.dll", StringComparison.OrdinalIgnoreCase));

        var union = FindUnion(editor, "UnityDrawer");
        var drawer = FindDrawerForTarget(editor, union);
        Assert.False(drawer.IsGenericType);
        AssertDrawerEntryPoints(drawer);
    }

    [Fact]
    public void GenericDrawer_UsesStandaloneOpenGenericTarget()
    {
        var result = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Generic_IntString"),
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = FindUnion(result, "UnityGeneric`1");
        var drawer = FindDrawerForTarget(result, union, requireUnboundGenericTarget: true);
        Assert.False(drawer.IsGenericType);
        AssertDrawerEntryPoints(drawer);
    }

    [Fact]
    public void ExplicitBoxedLayouts_AreAcceptedAndKeepCanonicalSerializedSchema()
    {
        var boxedFields = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_ExplicitBoxedFields"),
            includeEditorReferences: true);
        var boxedTuple = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_ExplicitBoxedTuple"),
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(boxedFields);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(boxedFields);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(boxedTuple);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(boxedTuple);

        var fieldsUnion = FindUnion(boxedFields, "UnityBoxedFieldsUnion");
        var tupleUnion = FindUnion(boxedTuple, "UnityBoxedTupleUnion");
        AssertCanonicalSerializedSchema(fieldsUnion);
        AssertCanonicalSerializedSchema(tupleUnion);

        var fieldsKind = fieldsUnion.GetTypeMembers("Kind").Single();
        var tupleKind = tupleUnion.GetTypeMembers("Kind").Single();
        Assert.Equal(
            fieldsKind.GetMembers().OfType<IFieldSymbol>().Select(field => field.Name),
            tupleKind.GetMembers().OfType<IFieldSymbol>().Select(field => field.Name));

        var fieldsPayload = fieldsUnion.GetMembers("_serializedPayload")
            .OfType<IFieldSymbol>()
            .Single();
        var tuplePayload = tupleUnion.GetMembers("_serializedPayload")
            .OfType<IFieldSymbol>()
            .Single();
        Assert.Equal(fieldsPayload.Type.Name, tuplePayload.Type.Name);

        Assert.Equal(
            PayloadFieldSignatures(fieldsUnion),
            PayloadFieldSignatures(tupleUnion));

        Assert.Equal(
            CountOccurrences(boxedFields.GeneratedSource, "CustomPropertyDrawer"),
            CountOccurrences(boxedTuple.GeneratedSource, "CustomPropertyDrawer"));
        Assert.Contains("OnBeforeSerialize", boxedFields.GeneratedSource);
        Assert.Contains("OnAfterDeserialize", boxedFields.GeneratedSource);
        Assert.Contains("OnBeforeSerialize", boxedTuple.GeneratedSource);
        Assert.Contains("OnAfterDeserialize", boxedTuple.GeneratedSource);
    }

    [Fact]
    public void ExplicitBoxedLayouts_SelectRuntimeStorageWithOneReferenceCache()
    {
        var fieldsResult = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_ExplicitBoxedFields"));
        var tupleResult = UnityGeneratorTestHarness.Run(
            UnityGeneratorTestHarness.LoadFixture("Serializable_ExplicitBoxedTuple"));

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(fieldsResult);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(fieldsResult);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(tupleResult);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(tupleResult);

        var fieldsType = EmitAndLoad(fieldsResult).GetType("UnityBoxedFieldsUnion");
        var tupleType = EmitAndLoad(tupleResult).GetType("UnityBoxedTupleUnion");
        Assert.NotNull(fieldsType);
        Assert.NotNull(tupleType);

        AssertCanonicalReflectionSchema(fieldsType!);
        AssertCanonicalReflectionSchema(tupleType!);

        var fieldsRuntimeSlots = RuntimeFields(fieldsType!)
            .Where(field =>
                field.FieldType.IsClass &&
                field.FieldType.DeclaringType == fieldsType)
            .ToArray();
        Assert.True(
            fieldsRuntimeSlots.Length >= 3,
            "BoxedFields did not retain object-backed per-variant slots; an Additive fallback can pass the serialized schema test.");

        var tupleRuntimeSlots = RuntimeFields(tupleType!)
            .Where(field => field.FieldType == typeof(object))
            .ToArray();
        Assert.Single(
            tupleRuntimeSlots);
        var tupleImplementationFields = RuntimeFields(tupleType!)
            .Where(field => field.Name != "_kind" &&
                field.Name != "_serializedPayload")
            .ToArray();
        Assert.DoesNotContain(
            tupleImplementationFields,
            field => field.Name is "__SpireSerializationState" or "__SpireSerializationOwner");
        Assert.All(
            tupleImplementationFields,
            field => Assert.True(
                field.FieldType == typeof(object),
                "BoxedTuple retained an unexpected runtime field: " + field.Name));
    }

    [Fact]
    public void ExplicitBoxedLayouts_GenericAndNestedDeclarationsCompileWithDrawers()
    {
        foreach (var (fixtureName, metadataName) in new[]
        {
            ("Serializable_BoxedFields_GenericIntString", "UnityBoxedFieldsGeneric`1"),
            ("Serializable_BoxedTuple_GenericIntString", "UnityBoxedTupleGeneric`1"),
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName),
                includeEditorReferences: true);

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
            var union = FindUnion(result, metadataName);
            var drawer = FindDrawerForTarget(
                result,
                union,
                requireUnboundGenericTarget: true);
            Assert.False(drawer.IsGenericType);
            AssertDrawerEntryPoints(drawer);
        }

        foreach (var (fixtureName, metadataName) in new[]
        {
            ("Serializable_BoxedFields_NestedContainingType", "UnityBoxedFieldsContainer+Nested"),
            ("Serializable_BoxedTuple_NestedContainingType", "UnityBoxedTupleContainer+Nested"),
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName),
                includeEditorReferences: true);

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
            var union = FindUnion(result, metadataName);
            var drawer = FindDrawerForTarget(result, union);
            Assert.False(drawer.IsGenericType);
            AssertDrawerEntryPoints(drawer);
        }
    }

    [Fact]
    public void ExplicitBoxedLayouts_CSharp9DoNotRequireIsExternalInit()
    {
        foreach (var fixtureName in new[]
        {
            "Serializable_ExplicitBoxedFields",
            "Serializable_ExplicitBoxedTuple",
            "Serializable_BoxedFields_GenericIntString",
            "Serializable_BoxedTuple_GenericIntString",
            "Serializable_BoxedFields_NestedContainingType",
            "Serializable_BoxedTuple_NestedContainingType",
        })
        {
            var result = UnityGeneratorTestHarness.Run(
                UnityGeneratorTestHarness.LoadFixture(fixtureName),
                languageVersion: LanguageVersion.CSharp9);

            UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
            UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
            Assert.DoesNotContain("IsExternalInit", result.GeneratedSource);
            Assert.Empty(GeneratedNullableWarnings(result));
        }
    }

    private static void AssertCanonicalSerializedSchema(INamedTypeSymbol union)
    {
        var kind = union.GetMembers("_kind").OfType<IFieldSymbol>().Single();
        var payload = union.GetMembers("_serializedPayload")
            .OfType<IFieldSymbol>()
            .Single();

        Assert.True(HasAttribute(kind, "System.NonSerializedAttribute"));
        Assert.False(HasAttribute(kind, "UnityEngine.SerializeField"));
        Assert.True(HasAttribute(payload, "UnityEngine.SerializeReference"));
        Assert.False(HasAttribute(payload, "System.NonSerializedAttribute"));

        var serializedFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                (HasAttribute(field, "UnityEngine.SerializeField") ||
                    HasAttribute(field, "UnityEngine.SerializeReference")))
            .ToArray();
        Assert.Single(serializedFields);
        Assert.Equal("_serializedPayload", serializedFields[0].Name);

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
                "Runtime field '" + field.Name + "' must be explicitly non-serialized."));

        Assert.Contains(
            union.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "OnBeforeSerialize");
        Assert.Contains(
            union.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "OnAfterDeserialize");
    }

    private static void AssertCanonicalReflectionSchema(Type unionType)
    {
        var fields = RuntimeFields(unionType);
        var serialized = fields.Where(field =>
            HasAttribute(field, "UnityEngine.SerializeField") ||
            HasAttribute(field, "UnityEngine.SerializeReference")).ToArray();
        Assert.Single(serialized);
        Assert.Contains(serialized, field => field.Name == "_serializedPayload");
        var kind = fields.Single(field => field.Name == "_kind");
        Assert.True(HasAttribute(kind, "System.NonSerializedAttribute"));
        Assert.DoesNotContain(serialized, field => field.Name == "_kind");
        Assert.All(
            fields.Where(field => field.Name != "_kind" && field.Name != "_serializedPayload"),
            field => Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                "Runtime field '" + field.Name + "' must be explicitly non-serialized."));
    }

    private static string[] PayloadFieldSignatures(INamedTypeSymbol union)
        => union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(field => !field.IsStatic)
            .Select(field => field.Name + ":" + NormalizePayloadType(field.Type.ToDisplayString()))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

    private static string[] PayloadTypeFieldSignatures(INamedTypeSymbol payloadType)
        => InstancePayloadFields(payloadType)
            .Select(field => field.Name + ":" + NormalizePayloadType(field.Type.ToDisplayString()))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

    private static INamedTypeSymbol[] SerializablePayloadTypes(INamedTypeSymbol union)
        => union.GetTypeMembers()
            .Where(type => HasAttribute(type, "System.SerializableAttribute"))
            .ToArray();

    private static IFieldSymbol[] InstancePayloadFields(INamedTypeSymbol payloadType)
        => payloadType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic)
            .ToArray();

    private static void AssertPayloadFirstSchema(INamedTypeSymbol union)
    {
        var kind = FindField(union, "_kind");
        var payload = FindField(union, "_serializedPayload");
        Assert.NotNull(kind);
        Assert.NotNull(payload);
        Assert.True(HasAttribute(kind!, "System.NonSerializedAttribute"));
        Assert.False(HasAttribute(kind!, "UnityEngine.SerializeField"));
        Assert.False(HasAttribute(kind!, "UnityEngine.SerializeReference"));
        Assert.True(HasAttribute(payload!, "UnityEngine.SerializeReference"));
        Assert.False(HasAttribute(payload!, "UnityEngine.SerializeField"));

        var serializedOwnerFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                (HasAttribute(field, "UnityEngine.SerializeField") ||
                    HasAttribute(field, "UnityEngine.SerializeReference")))
            .ToArray();
        Assert.Single(serializedOwnerFields);
        Assert.Equal("_serializedPayload", serializedOwnerFields[0].Name);

        var runtimeFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic && field.Name != "_serializedPayload")
            .ToArray();
        Assert.NotEmpty(runtimeFields);
        Assert.All(
            runtimeFields,
            field => Assert.True(
                HasAttribute(field, "System.NonSerializedAttribute"),
                $"Runtime field '{field.Name}' must be explicitly non-serialized."));

        var payloadTypes = SerializablePayloadTypes(union);
        var concretePayloadTypes = payloadTypes
            .Where(type => !type.IsAbstract)
            .ToArray();
        Assert.NotEmpty(concretePayloadTypes);
        Assert.Contains(
            concretePayloadTypes,
            type => InstancePayloadFields(type).Length == 0);

        var payloadFields = concretePayloadTypes
            .SelectMany(InstancePayloadFields)
            .ToArray();
        Assert.NotEmpty(payloadFields);
        Assert.All(payloadFields, field =>
        {
            Assert.Equal(Accessibility.Public, field.DeclaredAccessibility);
            Assert.True(
                HasAttribute(field, "UnityEngine.SerializeField"),
                $"Payload field '{field.ContainingType.Name}.{field.Name}' must carry SerializeField.");
        });
    }

    private static string NormalizePayloadType(string type)
        => type
            .Replace("UnityBoxedFieldsData", "<Data>", StringComparison.Ordinal)
            .Replace("UnityBoxedTupleData", "<Data>", StringComparison.Ordinal);

    private static FieldInfo[] RuntimeFields(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => !field.IsStatic)
            .ToArray();

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

    private static Assembly EmitAndLoad(UnityGeneratorRun result)
    {
        var compilation = Assert.IsType<CSharpCompilation>(result.OutputCompilation);
        return UnityGeneratorTestHarness.EmitAndLoad(
            compilation,
            "Generated allocation probe");
    }

    private static long InvokeAllocationProfile(Assembly assembly, string methodName, int iterations)
    {
        var harness = assembly.GetType("UnityAllocationProbeHarness");
        Assert.NotNull(harness);
        var method = harness!.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(int) },
            modifiers: null);
        Assert.NotNull(method);
        var result = method!.Invoke(null, new object[] { iterations });
        return Assert.IsType<long>(result);
    }

    private static Microsoft.CodeAnalysis.Diagnostic[] GeneratedNullableWarnings(UnityGeneratorRun result)
    {
        var generatedTrees = result.RunResult.GeneratedTrees.ToArray();
        return result.OutputCompilation
            .GetDiagnostics()
            .Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning &&
                diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal) &&
                diagnostic.Location.IsInSource &&
                generatedTrees.Any(tree =>
                    ReferenceEquals(tree, diagnostic.Location.SourceTree) ||
                    (tree.FilePath.Length > 0 && tree.FilePath == diagnostic.Location.SourceTree?.FilePath)))
            .ToArray();
    }

    private static INamedTypeSymbol FindUnion(
        UnityGeneratorRun result,
        string metadataName = "UnityBasic")
    {
        var union = result.OutputCompilation.GetTypeByMetadataName(metadataName);
        Assert.True(union is not null, $"Union '{metadataName}' was not generated.");
        return union!;
    }

    private static IFieldSymbol? FindField(INamedTypeSymbol type, string name)
        => type.GetMembers(name).OfType<IFieldSymbol>().SingleOrDefault();

    private static bool HasAttribute(ISymbol symbol, string suffix)
        => symbol.GetAttributes().Any(attribute =>
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            return name is not null &&
                (name == suffix || name.EndsWith(suffix, StringComparison.Ordinal));
        });

    private static bool IsNamedType(ITypeSymbol type, string displayName)
        => type is INamedTypeSymbol named &&
            named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::" + displayName;

    private static INamedTypeSymbol FindDrawerForTarget(
        UnityGeneratorRun result,
        INamedTypeSymbol target,
        bool requireUnboundGenericTarget = false)
    {
        var matching = GetAllTypes(result.OutputCompilation.GlobalNamespace)
            .Where(type => !type.IsImplicitlyDeclared)
            .Where(type => type.GetAttributes().Any(attribute =>
                IsCustomPropertyDrawer(attribute) &&
                DrawerTargetMatches(attribute, target, requireUnboundGenericTarget)))
            .ToArray();

        Assert.Single(matching);
        Assert.True(
            matching.Length == 1,
            "Expected exactly one compiled CustomPropertyDrawer for the generated union.");
        return matching[0];
    }

    private static bool IsCustomPropertyDrawer(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
            "UnityEditor.CustomPropertyDrawer";

    private static bool DrawerTargetMatches(
        AttributeData attribute,
        INamedTypeSymbol target,
        bool requireUnboundGenericTarget)
    {
        var targetArgument = attribute.ConstructorArguments
            .FirstOrDefault(argument => argument.Kind == TypedConstantKind.Type);
        if (targetArgument.Kind != TypedConstantKind.Type ||
            targetArgument.Value is not INamedTypeSymbol drawerTarget)
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

    private static void AssertDrawerEntryPoints(INamedTypeSymbol drawer)
    {
        Assert.Contains(
            drawer.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "CreatePropertyGUI" &&
                method.IsOverride &&
                method.Parameters.Length == 1);
        Assert.Contains(
            drawer.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "OnGUI" &&
                method.IsOverride &&
                method.Parameters.Length == 3);
        Assert.Contains(
            drawer.GetMembers().OfType<IMethodSymbol>(),
            method => method.Name == "GetPropertyHeight" &&
                method.IsOverride &&
                method.Parameters.Length == 2);
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
