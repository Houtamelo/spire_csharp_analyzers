using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Unity;

/// Unity-backed contracts for authored record-struct variant payloads.
[Trait("Environment", "Unity")]
public sealed class RecordVariantUnityTests
{
    [Fact]
    public async Task PartialRecord_GeneratesSerializablePayloadMembers()
    {
        var result = UnityGeneratorTestHarness.Run(
            PartialRecordSource(),
            includeUnityReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var slider = result.OutputCompilation.GetTypeByMetadataName("Customization+Slider");
        Assert.NotNull(slider);
        Assert.Contains(slider!.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.SerializableAttribute");
        Assert.Equal(
            new[] { "kind", "value" },
            slider.GetMembers().OfType<IPropertySymbol>()
                .Where(property => property.IsImplicitlyDeclared || property.DeclaredAccessibility == Accessibility.Public)
                .Select(property => property.Name)
                .Where(name => name is "kind" or "value")
                .ToArray());

        foreach (var name in new[] { "kind", "value" })
        {
            var field = FindPositionalBackingField(slider, name);
            Assert.Equal($"<{name}>k__BackingField", field.Name);
            Assert.True(HasAttribute(field, "UnityEngine.SerializeField"));
            Assert.False(HasAttribute(field, "UnityEngine.SerializeReference"));
        }

        var analyzerDiagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);
        Assert.DoesNotContain(analyzerDiagnostics, diagnostic =>
            diagnostic.Id is "SPIRE028" or "SPIRE029");

        var generated = result.GeneratedSource;
        Assert.Contains("partial record struct Slider", generated, StringComparison.Ordinal);
        Assert.Contains("SerializeField", generated, StringComparison.Ordinal);
        Assert.Contains("Customization.Slider", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialRecord_ManagedReferenceMarkersPreserveReferenceFields()
    {
        var result = UnityGeneratorTestHarness.Run(
            ManagedReferenceSource(),
            includeUnityReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        AssertNoDuplicateOrTargetWarnings(result.OutputCompilation);

        foreach (var name in new[] { "SpireMarker", "UnityMarker", "BothMarkers" })
        {
            var record = result.OutputCompilation.GetTypeByMetadataName($"ManagedCustomization+{name}");
            Assert.NotNull(record);
            var field = FindPositionalBackingField(record!, "value");
            Assert.Equal("<value>k__BackingField", field.Name);
            Assert.True(HasAttribute(field, "UnityEngine.SerializeReference"));
            Assert.False(HasAttribute(field, "UnityEngine.SerializeField"));
        }

        var generated = result.GeneratedSource;
        Assert.Contains("partial record struct SpireMarker", generated, StringComparison.Ordinal);
        Assert.Contains("partial record struct UnityMarker", generated, StringComparison.Ordinal);
        Assert.Contains("partial record struct BothMarkers", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedReferenceMarkers_RecognizeAliasesAndFullySuffixedAttributes()
    {
        var result = UnityGeneratorTestHarness.Run(
            ManagedReferenceAliasSource(),
            includeUnityReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        AssertNoDuplicateOrTargetWarnings(result.OutputCompilation);

        var fieldAlias = result.OutputCompilation.GetTypeByMetadataName(
            "ManagedAliasCustomization+FieldAlias");
        var referenceAlias = result.OutputCompilation.GetTypeByMetadataName(
            "ManagedAliasCustomization+ReferenceAlias");
        var bothAlias = result.OutputCompilation.GetTypeByMetadataName(
            "ManagedAliasCustomization+BothAlias");
        Assert.NotNull(fieldAlias);
        Assert.NotNull(referenceAlias);
        Assert.NotNull(bothAlias);

        var fieldAliasField = FindPositionalBackingField(fieldAlias!, "value");
        Assert.Equal("<value>k__BackingField", fieldAliasField.Name);
        Assert.True(HasAttribute(fieldAliasField, "UnityEngine.SerializeField"));
        Assert.False(HasAttribute(fieldAliasField, "UnityEngine.SerializeReference"));

        foreach (var record in new[] { referenceAlias!, bothAlias! })
        {
            var field = FindPositionalBackingField(record, "value");
            Assert.Equal("<value>k__BackingField", field.Name);
            Assert.True(HasAttribute(field, "UnityEngine.SerializeReference"));
            Assert.False(HasAttribute(field, "UnityEngine.SerializeField"));
        }
    }

    [Fact]
    public void ExistingAuthoredFieldAttributes_AreRetainedWithoutDuplicateGeneratedFields()
    {
        var result = UnityGeneratorTestHarness.Run(
            AuthoredAttributesSource(),
            includeUnityReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var slider = result.OutputCompilation.GetTypeByMetadataName("Customization+Slider");
        Assert.NotNull(slider);
        var valueProperty = slider!.GetMembers("value").OfType<IPropertySymbol>().Single();
        var valueField = FindPositionalBackingField(slider, "value");
        Assert.True(HasAttribute(valueField, "UnityEngine.SerializeField"));
        Assert.False(HasAttribute(valueField, "UnityEngine.SerializeReference"));
        Assert.Equal("<value>k__BackingField", valueField.Name);
        Assert.Equal(Accessibility.Public, valueProperty.DeclaredAccessibility);
        Assert.NotNull(valueProperty.SetMethod);
        AssertNoDuplicateOrTargetWarnings(result.OutputCompilation);

        var generated = result.GeneratedSource;
        Assert.Equal(1, generated.Split("partial record struct Slider", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("public int value { get; set; } = value;", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void InternalSerializationWrapper_UsesAuthoredRecordPayload()
    {
        var result = UnityGeneratorTestHarness.Run(
            FullyAnnotatedSource(),
            includeUnityReferences: true,
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var wrapper = result.OutputCompilation.GetTypeByMetadataName(
            "CustomizationTypes+SerializedPayloads+SliderSerializedPayload");
        Assert.NotNull(wrapper);
        Assert.NotNull(wrapper!.BaseType);
        Assert.Contains(
            wrapper.BaseType!.TypeArguments,
            type => type.ToDisplayString().EndsWith("Customization.Slider", StringComparison.Ordinal));

        var generated = result.GeneratedSource;
        Assert.Contains("Customization.Slider", generated, StringComparison.Ordinal);
        Assert.Contains("SerializedPayloads", generated, StringComparison.Ordinal);
        Assert.Contains("VariantMetadata", generated, StringComparison.Ordinal);
        Assert.Contains("Customization+Slider", generated, StringComparison.Ordinal);
        Assert.Contains("<kind>k__BackingField", generated, StringComparison.Ordinal);
        Assert.Contains("<value>k__BackingField", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void FormerName_RemainsSerializationOnlyForAuthoredPayload()
    {
        var result = UnityGeneratorTestHarness.Run(
            FormerNameSource(),
            includeUnityReferences: true,
            includeEditorReferences: true);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Customization");
        Assert.NotNull(union);
        var kind = union!.GetTypeMembers("Kind").Single();
        Assert.DoesNotContain(kind.GetMembers(), member => member.Name == "OldSlider");
        Assert.Empty(union.GetMembers("OldSlider"));

        var generated = result.GeneratedSource;
        Assert.Contains("OldSlider", generated, StringComparison.Ordinal);
        Assert.Contains("SerializedPayload", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordVariantAnalyzers_ReportCustomReconstructionWarning()
    {
        var result = UnityGeneratorTestHarness.Run(
            CustomMemberSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE029");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_DoNotWarnForGeneratedOrComputedMembers()
    {
        var result = UnityGeneratorTestHarness.Run(
            ComputedMemberSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE029");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_ReportNonPartialMissingTypeAndFields()
    {
        var result = UnityGeneratorTestHarness.Run(
            NonPartialMissingAllSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_ReportNonPartialMissingFields()
    {
        var result = UnityGeneratorTestHarness.Run(
            NonPartialMissingFieldsSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_ReportNonPartialMissingType()
    {
        var result = UnityGeneratorTestHarness.Run(
            NonPartialMissingTypeSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_DoNotWarnForFullyAnnotatedNonPartialRecord()
    {
        var result = UnityGeneratorTestHarness.Run(
            FullyAnnotatedSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_ReportMissingAttributeOnAuthoredPartialProperty()
    {
        var result = UnityGeneratorTestHarness.Run(
            PartialAuthoredPropertyMissingSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_DoNotWarnForAnnotatedAuthoredPartialProperty()
    {
        var result = UnityGeneratorTestHarness.Run(
            AuthoredAttributesSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_FieldlessNonPartialRecordNeedsOnlyTypeAttribute()
    {
        var result = UnityGeneratorTestHarness.Run(
            FieldlessMissingTypeSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public async Task RecordVariantAnalyzers_NonSerializableUnionIsAControl()
    {
        var result = UnityGeneratorTestHarness.Run(
            NonSerializableUnionSource(),
            includeUnityReferences: true);
        var diagnostics = await RunRecordVariantAnalyzers(result.OutputCompilation);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");
    }

    [Fact]
    public void RecordPayload_PrepareHydrateRetainsPositionalFieldsAndExcludesExtraState()
    {
        var result = UnityGeneratorTestHarness.Run(
            RuntimeRoundTripSource(),
            includeUnityReferences: true,
            includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "record variant serialization roundtrip");
        var union = assembly.GetType("Customization", throwOnError: true)!;
        var probe = assembly.GetType("RecordProbe", throwOnError: true)!;
        var left = probe.GetMethod("MakeLeft", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;
        var right = probe.GetMethod("MakeRight", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());

        var prepared = InvokeInstance(left, "__SpirePrepareForSerialization");
        var hydrated = InvokeInstance(prepared, "__SpireHydrateSerialized");
        var payload = union.GetProperty("slider")!.GetValue(hydrated)!;
        Assert.Equal(42, payload.GetType().GetProperty("value")!.GetValue(payload));
        Assert.Equal(0, payload.GetType().GetProperty("Extra")!.GetValue(payload));
        Assert.True(hydrated.Equals(right));
        Assert.Equal(left.GetHashCode(), hydrated.GetHashCode());

        var stale = probe.GetMethod("MakeStale", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;
        SetSerializedPayload(union, stale, GetSerializedPayload(union, prepared));
        Assert.True(stale.Equals(left));
        Assert.Equal(stale.GetHashCode(), left.GetHashCode());
    }

    [Fact]
    public void RecordPayload_FormerWrapperRestoresCanonicalRecordPayload()
    {
        var result = UnityGeneratorTestHarness.Run(
            RuntimeAliasSource(),
            includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "record variant former wrapper restore");
        var union = assembly.GetType("Customization", throwOnError: true)!;
        var oldWrapper = assembly.GetType(
            "CustomizationTypes+SerializedPayloads+OldSliderSerializedPayload",
            throwOnError: true)!;
        var wrapper = Activator.CreateInstance(oldWrapper)!;
        var valueField = oldWrapper.GetField(
            "value",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var oldPayload = Activator.CreateInstance(valueField.FieldType, new object[] { 42 })!;
        valueField.SetValue(wrapper, oldPayload);

        var restored = union.GetMethod(
                "__SpireFromSerialized",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, new[] { wrapper })!;
        Assert.Equal("Slider", union.GetProperty("kind")!.GetValue(restored)!.ToString());
        var payload = union.GetProperty("slider")!.GetValue(restored)!;
        Assert.Equal(42, payload.GetType().GetProperty("value")!.GetValue(payload));
    }

    [Fact]
    public void PartialRecord_PositionalValuesSurviveConstructionConversionAndHydration()
    {
        var result = UnityGeneratorTestHarness.Run(
            PartialRecordRuntimeSource(),
            includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var assembly = UnityGeneratorTestHarness.EmitAndLoad(
            result.OutputCompilation,
            "partial record positional values");
        var payloadType = assembly.GetType("Customization+Slider", throwOnError: true)!;
        var unionType = assembly.GetType("Customization", throwOnError: true)!;
        var probeType = assembly.GetType("PartialRecordProbe", throwOnError: true)!;

        var payload = Activator.CreateInstance(
            payloadType,
            new object?[] { 42, "slider" })!;
        Assert.Equal(42, payloadType.GetProperty("value")!.GetValue(payload));
        Assert.Equal("slider", payloadType.GetProperty("text")!.GetValue(payload));

        var union = probeType.GetMethod("MakeUnion")!.Invoke(null, null)!;
        var unionPayload = unionType.GetProperty("slider")!.GetValue(union)!;
        Assert.Equal(42, unionPayload.GetType().GetProperty("value")!.GetValue(unionPayload));
        Assert.Equal("slider", unionPayload.GetType().GetProperty("text")!.GetValue(unionPayload));

        var prepared = InvokeInstance(union, "__SpirePrepareForSerialization");
        var hydrated = InvokeInstance(prepared, "__SpireHydrateSerialized");
        var hydratedPayload = unionType.GetProperty("slider")!.GetValue(hydrated)!;
        Assert.Equal(42, hydratedPayload.GetType().GetProperty("value")!.GetValue(hydratedPayload));
        Assert.Equal("slider", hydratedPayload.GetType().GetProperty("text")!.GetValue(hydratedPayload));
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>>
        RunRecordVariantAnalyzers(Compilation compilation)
    {
        var analyzers = typeof(DiscriminatedUnionGenerator).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttributes(typeof(DiagnosticAnalyzerAttribute), inherit: false).Length > 0)
            .Select(type => Activator.CreateInstance(type) as DiagnosticAnalyzer)
            .Where(analyzer => analyzer is not null)
            .Select(analyzer => analyzer!)
            .Where(analyzer => analyzer.SupportedDiagnostics.Any(descriptor =>
                descriptor.Id is "SPIRE028" or "SPIRE029"))
            .ToImmutableArray();

        if (analyzers.Length == 0)
            return ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty;

        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE028" or "SPIRE029")
            .ToImmutableArray();
    }

    private static IFieldSymbol FindPositionalBackingField(
        INamedTypeSymbol record,
        string propertyName)
        => record.GetMembers()
            .OfType<IFieldSymbol>()
            .Single(field => field.AssociatedSymbol is IPropertySymbol property &&
                property.Name == propertyName);

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName);

    private static void AssertNoDuplicateOrTargetWarnings(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Id is "CS0657" or "CS0579")
            .ToArray();
        Assert.Empty(diagnostics);
    }

    private static object InvokeInstance(object value, string methodName)
        => value.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(value, null)!;

    private static object? GetSerializedPayload(Type unionType, object value)
        => unionType.GetField(
            "_serializedPayload",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(value);

    private static void SetSerializedPayload(Type unionType, object value, object? payload)
        => unionType.GetField(
            "_serializedPayload",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(value, payload);

    private static string PartialRecordSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        public enum SliderKind { Horizontal }

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public partial record struct Slider(SliderKind kind, int value);
        }
        """;

    private static string ManagedReferenceSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable] public class NodeBase { public int id; }

        [Serializable, DiscriminatedUnion]
        public partial struct ManagedCustomization
        {
            [Variant]
            public partial record struct SpireMarker([UnitySerializeReference] NodeBase? value);

            [Variant]
            public partial record struct UnityMarker([field: SerializeReference] NodeBase? value);

            [Variant]
            public partial record struct BothMarkers(
                [UnitySerializeReference]
                [field: SerializeReference]
                NodeBase? value);
        }
        """;

    private static string ManagedReferenceAliasSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        using SerializeFieldAlias = UnityEngine.SerializeField;
        using SerializeReferenceAlias = UnityEngine.SerializeReference;

        [Serializable] public class NodeBase { public int id; }

        [Serializable, DiscriminatedUnion]
        public partial struct ManagedAliasCustomization
        {
            [Variant]
            public partial record struct FieldAlias(
                [field: SerializeFieldAlias]
                int value);

            [Variant]
            public partial record struct ReferenceAlias(
                [field: SerializeReferenceAlias]
                NodeBase? value);

            [Variant]
            public partial record struct BothAlias(
                [UnitySerializeReferenceAttribute]
                [field: UnityEngine.SerializeReference]
                NodeBase? value);
        }
        """;

    private static string AuthoredAttributesSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        public enum SliderKind { Horizontal }

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            [Serializable]
            public partial record struct Slider(SliderKind kind, int value)
            {
                [field: SerializeField]
                public int value { get; set; } = value;
            }
        }
        """;

    private static string FullyAnnotatedSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        public enum SliderKind { Horizontal }

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            [Serializable]
            public record struct Slider(
                [field: SerializeField] SliderKind kind,
                [field: SerializeField] int value);
        }
        """;

    private static string FormerNameSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        public enum SliderKind { Horizontal }

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant(FormerlySerializedAs = "OldSlider")]
            [Serializable]
            public record struct Slider([field: SerializeField] SliderKind kind);
        }
        """;

    private static string CustomMemberSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public record struct Counter(int value)
            {
                public int value { get; init; } = value + 1;
                public int Double => value * 2;
            }
        }
        """;

    private static string ComputedMemberSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public record struct Counter(int value)
            {
                public int Double => value * 2;
            }
        }
        """;

    private static string NonPartialMissingAllSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public record struct Slider(int value);
        }
        """;

    private static string NonPartialMissingFieldsSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            [Serializable]
            public record struct Slider(int value);
        }
        """;

    private static string NonPartialMissingTypeSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public record struct Slider([field: SerializeField] int value);
        }
        """;

    private static string PartialAuthoredPropertyMissingSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            [Serializable]
            public partial record struct Slider(int value)
            {
                public int value { get; set; } = value;
            }
        }
        """;

    private static string FieldlessMissingTypeSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public record struct Empty;
        }
        """;

    private static string NonSerializableUnionSource() => """
        #nullable enable
        using Houtamelo.Spire;
        using UnityEngine;

        [DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public record struct Slider(int value);
        }
        """;

    private static string RuntimeRoundTripSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant(FormerlySerializedAs = "OldSlider")]
            [Serializable]
            public record struct Slider([field: SerializeField] int value)
            {
                public int Extra { get; init; }
            }
        }

        public static class RecordProbe
        {
            public static Customization MakeLeft()
                => new Customization.Slider(42) with { Extra = 99 };

            public static Customization MakeRight()
                => new Customization.Slider(42) with { Extra = 7 };

            public static Customization MakeStale()
                => new Customization.Slider(42);
        }
        """;

    private static string RuntimeAliasSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant(FormerlySerializedAs = "OldSlider")]
            [Serializable]
            public record struct Slider([field: SerializeField] int value);
        }
        """;

    private static string PartialRecordRuntimeSource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;

        [Serializable, DiscriminatedUnion]
        public partial struct Customization
        {
            [Variant]
            public partial record struct Slider(int value, string text);
        }

        public static class PartialRecordProbe
        {
            public static Customization MakeUnion()
                => new Customization.Slider(42, "slider");
        }
        """;
}
