using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.EnvironmentIsolation;

public sealed class EnvironmentIsolationTests
{
    [Fact]
    public void CoreClrSerializableUnion_EmitsCorePayloadMarkersWithoutUnitySurface()
    {
        var result = RunCore(CoreSource());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertPayloadSerializable(result.OutputCompilation, "CoreUnion", "Value");
        AssertPayloadSerializable(result.OutputCompilation, "CoreUnion", "Empty");
        AssertNoUnitySurface(result);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityMetadataWithoutUnitySymbol_RemainsCoreClr()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: true,
            includeEditorReferences: true,
            preprocessorSymbols: Array.Empty<string>());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertPayloadSerializable(result.OutputCompilation, "CoreUnion", "Value");
        AssertNoUnitySurface(result);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EditorSymbolWithoutUnityEnvironment_RemainsCoreClr()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: true,
            includeEditorReferences: true,
            preprocessorSymbols: new[] { "UNITY_EDITOR" });

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void ForeignSerializableAttribute_DoesNotOptIntoCoreOrUnitySerialization()
    {
        var result = RunCore(ForeignSerializableSource());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertPayloadNotSerializable(result.OutputCompilation, "ForeignUnion", "Value");
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void CoreClrSerializableAlias_PreservesFormerPayloadWithoutUnity()
    {
        var result = RunCore(AliasedSource());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertPayloadSerializable(result.OutputCompilation, "AliasedUnion", "Current");
        AssertPayloadSerializable(result.OutputCompilation, "AliasedUnion", "Old");
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void CoreClrMalformedFormerlySerializedAs_ReportsDiagnosticWithoutCorruptingPayloads()
    {
        var result = RunCore(MalformedAliasSource());

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU017");
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Single(FindPayloads(result.OutputCompilation, "MalformedAliasUnion", "Old"));
        Assert.NotNull(FindPayload(result.OutputCompilation, "MalformedAliasUnion", "First"));
        Assert.NotNull(FindPayload(result.OutputCompilation, "MalformedAliasUnion", "Second"));
        AssertNoUnitySurface(result);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityPlayerSymbol_EmitsRuntimeSerializationWithoutEditorDrawer()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: true,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER" });

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("_serializedPayload", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("UnitySerializationPayloadBase", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityEditor.CustomPropertyDrawer", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(GeneratedTrees(result, "Spire.UnityDrawerSupport.generated.cs"));
        Assert.Empty(GeneratedTrees(result, ".UnityDrawer.g.cs"));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityRuntimeSymbolWithEditorMetadataWithoutEditorSymbol_EmitsNoDrawer()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: true,
            includeEditorReferences: true,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER" });

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityEditor.CustomPropertyDrawer", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(GeneratedTrees(result, "Spire.UnityDrawerSupport.generated.cs"));
        Assert.Empty(GeneratedTrees(result, ".UnityDrawer.g.cs"));
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityEditorSymbols_EmitRuntimeSerializationAndDrawerSupport()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: true,
            includeEditorReferences: true,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER", "UNITY_EDITOR" });

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.Contains("ISerializationCallbackReceiver", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("UnityEditor.CustomPropertyDrawer", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Single(GeneratedTrees(result, "Spire.UnityDrawerSupport.generated.cs"));
        Assert.Single(GeneratedTrees(result, ".UnityDrawer.g.cs"));
    }

    [Fact]
    public void UnitySymbolWithoutRuntimeMetadata_ReportsMissingUnityEnvironmentButKeepsCoreApi()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: false,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER" });

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU016");
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertPayloadSerializable(result.OutputCompilation, "CoreUnion", "Value");
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void UnityEditorSymbolWithoutRuntimeMetadata_ReportsMissingRuntimeAndNoDrawer()
    {
        var result = UnityGeneratorTestHarness.Run(
            CoreSource(),
            includeUnityReferences: false,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER", "UNITY_EDITOR" });

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU016");
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void CoreClrReadonlySerializableStruct_IsNotRejectedAsUnityShape()
    {
        var result = RunCore(ReadonlySource());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU013");
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void CoreClrRefSerializableStruct_IsNotRejectedAsUnityShape()
    {
        var result = RunCore(RefStructSource());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU013");
        AssertNoUnitySurface(result);
    }

    [Fact]
    public void CoreClrSerializableRecord_IsNotRejectedAsUnityShape()
    {
        var result = RunCore(RecordSource());

        AssertNoGeneratorWarnings(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU013");
        AssertNoUnitySurface(result);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityReadonlySerializableStruct_IsRejectedWithoutRuntimeSurface()
    {
        AssertUnityShapeDiagnostic(ReadonlySource());
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityRefSerializableStruct_IsRejectedWithoutRuntimeSurface()
    {
        AssertUnityShapeDiagnostic(RefStructSource());
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnitySerializableRecord_IsRejectedWithoutRuntimeSurface()
    {
        AssertUnityShapeDiagnostic(RecordSource());
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void EnvironmentRunsDoNotLeakUnitySurfaceAcrossCompilations()
    {
        var driver = UnityGeneratorTestHarness.CreateDriver(Array.Empty<string>());
        var core = UnityGeneratorTestHarness.RunWithDriver(
            driver,
            CoreSource(),
            includeUnityReferences: false,
            includeEditorReferences: false,
            preprocessorSymbols: Array.Empty<string>(),
            out var nextDriver);
        driver = nextDriver;
        var editor = UnityGeneratorTestHarness.RunWithDriver(
            driver,
            CoreSource(),
            includeUnityReferences: true,
            includeEditorReferences: true,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER", "UNITY_EDITOR" },
            out nextDriver);
        driver = nextDriver;
        var coreAgain = UnityGeneratorTestHarness.RunWithDriver(
            driver,
            CoreSource(),
            includeUnityReferences: false,
            includeEditorReferences: false,
            preprocessorSymbols: Array.Empty<string>(),
            out nextDriver);

        AssertNoGeneratorWarnings(core);
        AssertNoGeneratorWarnings(editor);
        AssertNoGeneratorWarnings(coreAgain);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(core);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(editor);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(coreAgain);
        AssertNoUnitySurface(core);
        Assert.Contains("UnityEditor.CustomPropertyDrawer", editor.GeneratedSource, StringComparison.Ordinal);
        Assert.Single(GeneratedTrees(editor, "Spire.UnityDrawerSupport.generated.cs"));
        AssertNoUnitySurface(coreAgain);
    }

    private static UnityGeneratorRun RunCore(string source)
        => UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: false,
            preprocessorSymbols: Array.Empty<string>());

    private static void AssertUnityShapeDiagnostic(string source)
    {
        var result = UnityGeneratorTestHarness.Run(
            source,
            includeUnityReferences: true,
            preprocessorSymbols: new[] { "UNITY_5_3_OR_NEWER" });

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "SPIRE_DU013");
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoUnitySerializationSurface(result);
    }

    private static void AssertNoGeneratorWarnings(UnityGeneratorRun result)
    {
        var diagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.True(
            diagnostics.Length == 0,
            string.Join(
                Environment.NewLine,
                diagnostics.Select(diagnostic => diagnostic.Id + ": " + diagnostic.GetMessage())));
    }

    private static void AssertNoUnitySurface(UnityGeneratorRun result)
    {
        foreach (var marker in new[]
        {
            "ISerializationCallbackReceiver",
            "UnitySerializationPayloadBase",
            "_serializedPayload",
            "UnityEngine.SerializeField",
            "UnityEditor.CustomPropertyDrawer",
            "UnityEngine.Debug.Assert",
        })
        {
            Assert.DoesNotContain(marker, result.GeneratedSource, StringComparison.Ordinal);
        }

        Assert.Empty(GeneratedTrees(result, "Spire.UnitySerializationSupport.generated.cs"));
        Assert.Empty(GeneratedTrees(result, "Spire.UnityDrawerSupport.generated.cs"));
        Assert.Empty(GeneratedTrees(result, ".UnityDrawer.g.cs"));
    }

    private static void AssertNoUnitySerializationSurface(UnityGeneratorRun result)
    {
        foreach (var marker in new[]
        {
            "ISerializationCallbackReceiver",
            "UnitySerializationPayloadBase",
            "_serializedPayload",
            "UnityEngine.SerializeField",
            "UnityEditor.CustomPropertyDrawer",
        })
        {
            Assert.DoesNotContain(marker, result.GeneratedSource, StringComparison.Ordinal);
        }

        Assert.Empty(GeneratedTrees(result, "Spire.UnitySerializationSupport.generated.cs"));
        Assert.Empty(GeneratedTrees(result, "Spire.UnityDrawerSupport.generated.cs"));
        Assert.Empty(GeneratedTrees(result, ".UnityDrawer.g.cs"));
    }

    private static IReadOnlyList<SyntaxTree> GeneratedTrees(
        UnityGeneratorRun result,
        string hint)
        => result.RunResult.GeneratedTrees
            .Where(tree =>
                hint.StartsWith(".", StringComparison.Ordinal)
                    ? tree.FilePath.EndsWith(hint, StringComparison.Ordinal)
                    : string.Equals(
                        System.IO.Path.GetFileName(tree.FilePath),
                        hint,
                        StringComparison.Ordinal))
            .ToArray();

    private static void AssertPayloadSerializable(
        Compilation compilation,
        string unionName,
        string variantName)
    {
        var payload = FindPayload(compilation, unionName, variantName);
        Assert.NotNull(payload);
        Assert.True(payload!.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.SerializableAttribute"));
    }

    private static void AssertPayloadNotSerializable(
        Compilation compilation,
        string unionName,
        string variantName)
    {
        var payload = FindPayload(compilation, unionName, variantName);
        Assert.NotNull(payload);
        Assert.False(payload!.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.SerializableAttribute"));
    }

    private static INamedTypeSymbol? FindPayload(
        Compilation compilation,
        string unionName,
        string variantName)
        => AllNamedTypes(compilation.GlobalNamespace)
            .SingleOrDefault(type =>
                type.Name == variantName &&
                type.ContainingType?.Name == "Variants" &&
                type.ContainingType.ContainingType?.Name == unionName + "Types");

    private static IReadOnlyList<INamedTypeSymbol> FindPayloads(
        Compilation compilation,
        string unionName,
        string variantName)
        => AllNamedTypes(compilation.GlobalNamespace)
            .Where(type =>
                type.Name == variantName &&
                type.ContainingType?.Name == "Variants" &&
                type.ContainingType.ContainingType?.Name == unionName + "Types")
            .ToArray();

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in AllNamedTypes(type))
                yield return nested;
        }

        foreach (var child in root.GetNamespaceMembers())
        {
            foreach (var nested in AllNamedTypes(child))
                yield return nested;
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllNamedTypes(INamedTypeSymbol root)
    {
        foreach (var nested in root.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in AllNamedTypes(nested))
                yield return child;
        }
    }

    private static string CoreSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public partial struct CoreUnion
        {
            [Variant] public static partial CoreUnion Value(int value);
            [Variant] public static partial CoreUnion Empty();
        }
        """;

    private static string AliasedSource() => """
        using System;
        using SerializableAlias = System.SerializableAttribute;
        using Houtamelo.Spire;

        [SerializableAlias]
        [DiscriminatedUnion]
        public partial struct AliasedUnion
        {
            [Variant(FormerlySerializedAs = "Old")]
            public static partial AliasedUnion Current(int value);
        }
        """;

    private static string MalformedAliasSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public partial struct MalformedAliasUnion
        {
            [Variant(FormerlySerializedAs = "Old")]
            public static partial MalformedAliasUnion First(int value);

            [Variant(FormerlySerializedAs = "Old")]
            public static partial MalformedAliasUnion Second(int value);
        }
        """;

    private static string ForeignSerializableSource() => """
        using System;
        using ForeignSerializable = Foreign.SerializableAttribute;
        using Houtamelo.Spire;

        namespace Foreign
        {
            [AttributeUsage(AttributeTargets.Struct)]
            public sealed class SerializableAttribute : Attribute { }
        }

        [ForeignSerializable]
        [DiscriminatedUnion]
        public partial struct ForeignUnion
        {
            [Variant] public static partial ForeignUnion Value(int value);
        }
        """;

    private static string ReadonlySource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public readonly partial struct ReadonlyUnion
        {
            [Variant] public static partial ReadonlyUnion Value(int value);
        }
        """;

    private static string RefStructSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public ref partial struct RefUnion
        {
            [Variant] public static partial RefUnion Value(int value);
        }
        """;

    private static string RecordSource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable]
        [DiscriminatedUnion]
        public abstract partial record RecordUnion
        {
            public sealed partial record Value(int value) : RecordUnion;
        }
        """;
}
