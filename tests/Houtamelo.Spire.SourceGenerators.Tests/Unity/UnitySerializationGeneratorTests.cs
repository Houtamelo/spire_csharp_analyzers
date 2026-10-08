using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        Assert.True(HasAttribute(kindField, "UnityEngine.SerializeField"));
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
        Assert.Single(serializedFields);
        Assert.Equal("_kind", serializedFields[0].Name);
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
    public void GeneratedInitAndCallbacks_ExposeMutationAndInvalidStorageBranches()
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

        var internalSlots = union.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property =>
                property.Name.StartsWith("_s", StringComparison.Ordinal) &&
                property.DeclaredAccessibility == Accessibility.Internal)
            .ToArray();
        Assert.NotEmpty(internalSlots);
        Assert.All(internalSlots, property =>
        {
            Assert.NotNull(property.GetMethod);
            Assert.NotNull(property.SetMethod);
            Assert.True(property.SetMethod!.IsInitOnly);
        });

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
    public void ExplicitBoxedLayouts_SelectTheirRuntimeStorageInEmittedAssemblies()
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
        Assert.DoesNotContain(
            RuntimeFields(tupleType!),
            field => field.FieldType.IsValueType &&
                field.Name != "_kind");
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

        Assert.True(HasAttribute(kind, "UnityEngine.SerializeField"));
        Assert.True(HasAttribute(payload, "UnityEngine.SerializeReference"));
        Assert.False(HasAttribute(kind, "System.NonSerializedAttribute"));
        Assert.False(HasAttribute(payload, "System.NonSerializedAttribute"));

        var serializedFields = union.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic &&
                (HasAttribute(field, "UnityEngine.SerializeField") ||
                    HasAttribute(field, "UnityEngine.SerializeReference")))
            .ToArray();
        Assert.Equal(2, serializedFields.Length);

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
        Assert.Equal(2, serialized.Length);
        Assert.Contains(serialized, field => field.Name == "_kind");
        Assert.Contains(serialized, field => field.Name == "_serializedPayload");
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
