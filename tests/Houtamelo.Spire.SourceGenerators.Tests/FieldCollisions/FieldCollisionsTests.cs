using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldCollisions;

public sealed class FieldCollisionsTests : Behavioral.BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void IncompatibleFields_AreQualified_AndNoncollidingFieldsKeepTheirNames(
        string strategy)
    {
        var assembly = CompileAndLoad(IncompatibleFieldsSource(strategy));
        var unionType = GetType(assembly, "FieldCollisionUnion");

        var count = InvokeFactory(unionType, "Count", 7);
        var text = InvokeFactory(unionType, "Text", "hello");
        var tagged = InvokeFactory(unionType, "Tagged", true);

        Assert.Equal(7, ReadProperty(count, "value"));
        Assert.Equal("hello", ReadProperty(text, "value"));
        Assert.True((bool)ReadProperty(tagged, "tagged")!);
        Assert.Null(unionType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void ThreeWayCollision_QualifiesEveryParticipant_WithAcronymAwareNames(
        string strategy)
    {
        var assembly = CompileAndLoad(ThreeWayCollisionSource(strategy));
        var unionType = GetType(assembly, "ThreeWayCollisionUnion");

        var response = InvokeFactory(unionType, "HTTPResponse", 11);
        var url = InvokeFactory(unionType, "URL", "https://example.test");
        var json = InvokeFactory(unionType, "JsonValue", 3.5);
        var ready = InvokeFactory(unionType, "Ready", true);

        Assert.Equal(11, ReadProperty(ReadPayload(response, "httpResponse"), "value"));
        Assert.Equal("https://example.test", ReadProperty(ReadPayload(url, "url"), "value"));
        Assert.Equal(3.5, ReadProperty(ReadPayload(json, "jsonValue"), "value"));
        Assert.True((bool)ReadProperty(ready, "ready")!);
        Assert.Null(unionType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void KeywordVariantNames_EscapeQualifiedProperties(string strategy)
    {
        var assembly = CompileAndLoad(KeywordCollisionSource(strategy));
        var unionType = GetType(assembly, "KeywordCollisionUnion");

        var classValue = InvokeFactory(unionType, "class", 5);
        var eventValue = InvokeFactory(unionType, "event", "payload");

        Assert.Equal(5, ReadProperty(ReadPayload(classValue, "class_value"), "value"));
        Assert.Equal("payload", ReadProperty(ReadPayload(eventValue, "event_value"), "value"));
        Assert.Null(unionType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void SameReferenceNullability_MergesToNullableProperty_InEitherOrder()
    {
        foreach (var strategy in Layouts)
        {
            foreach (var nullableFirst in new[] { false, true })
            {
                var source = NullableReferenceSource(strategy, nullableFirst);
                var assembly = CompileAndLoad(source);
                var unionType = GetType(assembly, "NullableReferenceUnion");
                var effectType = GetType(assembly, "Effect");
                var effect = Activator.CreateInstance(effectType)!;

                var first = InvokeFactory(unionType, "First", effect);
                var second = InvokeFactory(unionType, "Second", (object)null!);

                Assert.Same(effect, ReadProperty(first, "effect"));
                Assert.Null(ReadProperty(second, "effect"));

                var compilation = Generate(source);
                var firstPayload = compilation.GetTypeByMetadataName("NullableReferenceUnionTypes+Variants+First");
                var secondPayload = compilation.GetTypeByMetadataName("NullableReferenceUnionTypes+Variants+Second");
                Assert.NotNull(firstPayload);
                Assert.NotNull(secondPayload);
                Assert.Equal(
                    nullableFirst ? NullableAnnotation.Annotated : NullableAnnotation.NotAnnotated,
                    Assert.Single(firstPayload!.GetMembers("effect").OfType<IPropertySymbol>()).NullableAnnotation);
                Assert.Equal(
                    nullableFirst ? NullableAnnotation.NotAnnotated : NullableAnnotation.Annotated,
                    Assert.Single(secondPayload!.GetMembers("effect").OfType<IPropertySymbol>()).NullableAnnotation);
            }
        }
    }

    [Fact]
    public void NestedReferenceNullability_MergesOuterAndInnerAnnotations()
    {
        foreach (var strategy in Layouts)
        {
            foreach (var nullableFirst in new[] { false, true })
            {
                var source = NestedNullableReferenceSource(strategy, nullableFirst);
                var compilation = Generate(source);
                var payload = compilation.GetTypeByMetadataName("NestedNullableReferenceUnionTypes+Variants+First");
                Assert.NotNull(payload);
                var field = Assert.Single(payload!.GetMembers("effects").OfType<IPropertySymbol>());
                Assert.Equal(NullableAnnotation.Annotated, field.NullableAnnotation);
                var listType = Assert.IsAssignableFrom<INamedTypeSymbol>(field.Type);
                var effectType = Assert.Single(listType.TypeArguments);
                Assert.Equal(
                    nullableFirst ? NullableAnnotation.Annotated : NullableAnnotation.NotAnnotated,
                    effectType.NullableAnnotation);
            }
        }
    }

    [Fact]
    public void TupleElementNames_DoNotSplitFields_AndRemainStable()
    {
        foreach (var strategy in Layouts)
        {
            foreach (var firstNames in new[] { false, true })
            {
                var source = TupleNullabilitySource(strategy, firstNames);
                var compilation = Generate(source);
                var payload = compilation.GetTypeByMetadataName("TupleNullabilityUnionTypes+Variants+First");
                Assert.NotNull(payload);
                var field = Assert.Single(payload!.GetMembers("value").OfType<IPropertySymbol>());
                var tupleType = Assert.IsAssignableFrom<INamedTypeSymbol>(field.Type);

                var expectedNames = firstNames
                    ? new[] { "firstEffect", "firstCount" }
                    : new[] { "secondEffect", "secondCount" };
                Assert.Equal(expectedNames, tupleType.TupleElements.Select(element => element.Name));
                Assert.Equal(
                    NullableAnnotation.Annotated,
                    tupleType.TupleElements[0].Type.NullableAnnotation);

                var assembly = CompileAndLoad(source);
                var unionType = GetType(assembly, "TupleNullabilityUnion");
                var effectType = GetType(assembly, "Effect");
                var effect = Activator.CreateInstance(effectType)!;
                var tupleTypeAtRuntime = typeof(ValueTuple<,>)
                    .MakeGenericType(effectType, typeof(int));
                var tuple = Activator.CreateInstance(
                    tupleTypeAtRuntime,
                    new object?[] { effect, 9 })!;
                var instance = InvokeFactory(unionType, "First", tuple);
                var value = ReadProperty(instance, "value")!;
                Assert.Same(effect, value.GetType().GetField("Item1")!.GetValue(value));
                Assert.Equal(9, value.GetType().GetField("Item2")!.GetValue(value));
            }
        }
    }

    [Fact]
    public void NestedGenericValueTypes_MergeReferenceNullabilityRecursively()
    {
        foreach (var strategy in GenericCapableStrategies)
        {
            var source = NestedGenericValueSource((string)strategy[0]);
            var compilation = Generate(source);
            var payload = compilation.GetTypeByMetadataName("NestedGenericValueUnionTypes+Variants+First");
            Assert.NotNull(payload);
            var field = Assert.Single(payload!.GetMembers("value").OfType<IPropertySymbol>());
            var outerType = Assert.IsAssignableFrom<INamedTypeSymbol>(field.Type);
            var innerType = Assert.IsAssignableFrom<INamedTypeSymbol>(
                Assert.Single(outerType.TypeArguments));
            var effectType = Assert.Single(innerType.TypeArguments);

            Assert.Equal("Outer", outerType.Name);
            Assert.Equal("Inner", innerType.Name);
            Assert.Equal(NullableAnnotation.Annotated, effectType.NullableAnnotation);

            var assembly = CompileAndLoad(source);
            var unionType = GetType(assembly, "NestedGenericValueUnion");
            var runtimeEffectType = GetType(assembly, "Effect");
            var effect = Activator.CreateInstance(runtimeEffectType)!;
            var innerRuntimeType = GetType(assembly, "Inner`1").MakeGenericType(runtimeEffectType);
            var outerRuntimeType = GetType(assembly, "Outer`1").MakeGenericType(innerRuntimeType);
            var inner = Activator.CreateInstance(innerRuntimeType, new object?[] { effect })!;
            var outer = Activator.CreateInstance(outerRuntimeType, new[] { inner })!;
            var instance = InvokeFactory(unionType, "First", outer);
            var value = ReadProperty(instance, "value")!;
            var innerValue = value.GetType().GetField("Value")!.GetValue(value)!;
            Assert.Same(effect, innerValue.GetType().GetField("Value")!.GetValue(innerValue));
        }
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void NullableValueArray_MergesContainerNullability(string strategy)
    {
        var source = NullableArraySource(strategy);
        var compilation = Generate(source);
        var payload = compilation.GetTypeByMetadataName("NullableArrayUnionTypes+Variants+First");
        var secondPayload = compilation.GetTypeByMetadataName("NullableArrayUnionTypes+Variants+Second");
        Assert.NotNull(payload);
        Assert.NotNull(secondPayload);
        var field = Assert.Single(payload!.GetMembers("values").OfType<IPropertySymbol>());
        var secondField = Assert.Single(secondPayload!.GetMembers("values").OfType<IPropertySymbol>());
        var arrayType = Assert.IsAssignableFrom<IArrayTypeSymbol>(field.Type);
        var nullableElement = Assert.IsAssignableFrom<INamedTypeSymbol>(arrayType.ElementType);

        Assert.Equal(NullableAnnotation.NotAnnotated, field.NullableAnnotation);
        Assert.Equal(NullableAnnotation.Annotated, secondField.NullableAnnotation);
        Assert.Equal(SpecialType.System_Nullable_T, nullableElement.OriginalDefinition.SpecialType);
        Assert.Equal(SpecialType.System_Int32, Assert.Single(nullableElement.TypeArguments).SpecialType);

        var assembly = CompileAndLoad(source);
        var unionType = GetType(assembly, "NullableArrayUnion");
        var values = Array.CreateInstance(typeof(int?), 1);
        values.SetValue(4, 0);
        var first = InvokeFactory(unionType, "First", values);
        var second = InvokeFactory(unionType, "Second", (object)null!);
        Assert.Equal(4, ((Array)ReadProperty(first, "values")!).GetValue(0));
        Assert.Null(ReadProperty(second, "values"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void AcronymDigitAndUnderscoreVariants_UseTheSameQualifiedNameAlgorithm(
        string strategy)
    {
        var assembly = CompileAndLoad(AcronymDigitSource(strategy));
        var unionType = GetType(assembly, "AcronymDigitUnion");

        var response = InvokeFactory(unionType, "HTTP2Response", 17);
        var url = InvokeFactory(unionType, "URL_V1", "v1");

        Assert.Equal(17, ReadProperty(ReadPayload(response, "http2Response"), "value"));
        Assert.Equal("v1", ReadProperty(ReadPayload(url, "url_V1"), "value"));
        Assert.Null(unionType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [MemberData(nameof(AllLayoutStrategies))]
    public void NoncollidingKeywordFields_AreUsableAcrossAllLayouts(string strategy)
    {
        var assembly = CompileAndLoad(KeywordFieldSource(strategy));
        var unionType = GetType(assembly, "KeywordFieldUnion");

        var eventValue = InvokeFactory(unionType, "First", 5);
        var classValue = InvokeFactory(unionType, "Second", "payload");
        var regularValue = InvokeFactory(unionType, "Third", true);

        Assert.Equal(5, ReadProperty(eventValue, "event"));
        Assert.Equal("payload", ReadProperty(classValue, "class"));
        Assert.True((bool)ReadProperty(regularValue, "regular")!);
    }

    [Theory]
    [MemberData(nameof(AllLayoutStrategies))]
    public void SystemTextJson_PreservesQualifiedFieldsAndKeywordVariants(string strategy)
    {
        var assembly = CompileAndLoad(JsonCollisionSource(strategy, "SystemTextJson"));
        var unionType = GetType(assembly, "JsonCollisionUnion");

        AssertSystemTextJsonRoundTrip(
            unionType,
            "Count",
            "value",
            12,
            12);
        AssertSystemTextJsonRoundTrip(
            unionType,
            "Text",
            "value",
            "hello",
            "hello");
        AssertSystemTextJsonRoundTrip(
            unionType,
            "event",
            "value",
            true,
            true,
            "event_value");
    }

    [Theory]
    [MemberData(nameof(AllLayoutStrategies))]
    public void NewtonsoftJson_PreservesQualifiedFieldsAndKeywordVariants(string strategy)
    {
        var assembly = CompileAndLoad(JsonCollisionSource(strategy, "NewtonsoftJson"));
        var unionType = GetType(assembly, "JsonCollisionUnion");

        AssertNewtonsoftJsonRoundTrip(
            unionType,
            "Count",
            "value",
            12,
            12);
        AssertNewtonsoftJsonRoundTrip(
            unionType,
            "Text",
            "value",
            "hello",
            "hello");
        AssertNewtonsoftJsonRoundTrip(
            unionType,
            "event",
            "value",
            true,
            true,
            "event_value");
    }

    [Theory]
    [MemberData(nameof(AllLayoutStrategies))]
    public void ToString_UsesQualifiedRuntimeProperties_AndOriginalParameterNames(
        string strategy)
    {
        var source = ToStringCollisionSource(strategy);
        var compilation = Generate(source);
        var union = GetUnion(compilation, "ToStringCollisionUnion");
        Assert.Equal(
            "displayName",
            Assert.Single(union.GetMembers("Count").OfType<IMethodSymbol>()).Parameters[0].Name);
        Assert.Equal(
            "displayName",
            Assert.Single(union.GetMembers("Text").OfType<IMethodSymbol>()).Parameters[0].Name);

        var assembly = CompileAndLoad(source);
        var unionType = GetType(assembly, "ToStringCollisionUnion");
        var count = InvokeFactory(unionType, "Count", 42);
        var text = InvokeFactory(unionType, "Text", "hello");

        Assert.Equal(42, ReadProperty(count, "displayName"));
        Assert.Equal("hello", ReadProperty(text, "displayName"));
        Assert.Equal("Count(42)", count.ToString());
        Assert.Equal("Text(hello)", text.ToString());
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void NullableValueTypes_RemainDistinctAndQualified(string strategy)
    {
        var assembly = CompileAndLoad(NullableValueSource(strategy));
        var unionType = GetType(assembly, "NullableValueUnion");

        var integer = InvokeFactory(unionType, "Integer", 3);
        var nullableInteger = InvokeFactory(unionType, "NullableInteger", (object)null!);

        Assert.Equal(3, ReadProperty(integer, "value"));
        Assert.Null(ReadProperty(nullableInteger, "value"));
        Assert.Null(unionType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Deconstruct_RemainsCompatibleForQualifiedSharedArity(string strategy)
    {
        var assembly = CompileAndLoad(IncompatibleFieldsSource(strategy));
        var unionType = GetType(assembly, "FieldCollisionUnion");

        var count = InvokeFactory(unionType, "Count", 19);
        var countPayload = ReadPayload(count, "count");
        var countDeconstruct = countPayload.GetType().GetMethod("Deconstruct")!;
        var countValue = new object?[] { null };
        countDeconstruct.Invoke(countPayload, countValue);
        Assert.Equal(19, countValue[0]);

        var text = InvokeFactory(unionType, "Text", "deconstructed");
        var textPayload = ReadPayload(text, "text");
        var textDeconstruct = textPayload.GetType().GetMethod("Deconstruct")!;
        var textValue = new object?[] { null };
        textDeconstruct.Invoke(textPayload, textValue);
        Assert.Equal("deconstructed", textValue[0]);
    }

    [Fact]
    public async Task QualifiedFields_AreMappedByFieldAccessAndTypeSafetyAnalyzers()
    {
        var diagnostics = await Analyze(
            AnalyzerSource,
            new FieldAccessSafetyAnalyzer(),
            new TypeSafetyAnalyzer());

        Assert.Empty(diagnostics.Where(diagnostic =>
            diagnostic.Id is "SPIRE011" or "SPIRE012" or "SPIRE013" or "SPIRE014"));
    }

    [Fact]
    public async Task QualifiedFields_RejectAccessThroughTheWrongVariant()
    {
        var diagnostics = await Analyze(
            WrongVariantAccessSource,
            new FieldAccessSafetyAnalyzer());

        var diagnostic = Assert.Single(diagnostics.Where(item => item.Id == "SPIRE013"));
        Assert.Contains("text", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QualifiedPropertyPatterns_AreExhaustiveWhenAllVariantsAreCovered()
    {
        var diagnostics = await Analyze(
            ExhaustivePropertyPatternSource,
            new ExhaustivenessAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE009");
    }

    [Fact]
    public async Task QualifiedPropertyPatterns_ReportMissingVariants()
    {
        var diagnostics = await Analyze(
            MissingVariantPropertyPatternSource,
            new ExhaustivenessAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SPIRE009");
    }

    [Fact]
    public void ExistingRecordAndClassUnionsRemainUnchanged()
    {
        var recordAssembly = CompileAndLoad(RecordUnionSource);
        var recordType = GetType(recordAssembly, "TestNs.Shape");
        Assert.NotNull(recordType.GetNestedType("Circle"));
        Assert.NotNull(recordType.GetNestedType("Square"));
        Assert.NotNull(recordType.GetNestedType("Point"));

        GeneratorTestHelper.RunGenerator(
            ClassUnionSource,
            out var classCompilation,
            out var classDiagnostics,
            path: "class-control.cs");
        var classDiagnostic = Assert.Single(classDiagnostics);
        Assert.Equal("SPIRE_DU011", classDiagnostic.Id);
        GeneratorTestHelper.AssertNoCompilationErrors(classCompilation);
    }

    [Fact]
    public void RecordFieldTypeConflicts_RetainTheExistingDiagnostic()
    {
        GeneratorTestHelper.RunGenerator(
            RecordFieldConflictSource,
            out var compilation,
            out var diagnostics,
            path: "record-field-conflict-control.cs");

        var diagnostic = Assert.Single(diagnostics.Where(item => item.Id == "SPIRE_DU010"));
        Assert.Contains("value", diagnostic.GetMessage(), StringComparison.Ordinal);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
    }

    [Fact]
    public void OverloadedVariantNames_DoNotCrashTheGenerator()
    {
        var exception = Record.Exception(() =>
            GeneratorTestHelper.RunGenerator(
                OverloadedVariantSource,
                out _,
                out _,
                path: "overloaded-variant-control.cs"));

        Assert.Null(exception);
    }

    private static readonly string[] Layouts =
    {
        "ReferenceStorage = ReferenceStorage.Auto",
        "ReferenceStorage = ReferenceStorage.Typed",
        "ReferenceStorage = ReferenceStorage.Shared",
    };

    private static readonly string[] AllLayouts =
    {
        "ReferenceStorage = ReferenceStorage.Auto",
        "ReferenceStorage = ReferenceStorage.Typed",
        "ReferenceStorage = ReferenceStorage.Shared",
    };

    public static IEnumerable<object[]> AllLayoutStrategies
        => AllLayouts.Select(strategy => new object[] { strategy });

    private static void AssertSystemTextJsonRoundTrip(
        Type unionType,
        string variantName,
        string propertyName,
        object argument,
        object expected,
        string? payloadAccessor = null)
    {
        var instance = InvokeFactory(unionType, variantName, argument);
        var json = System.Text.Json.JsonSerializer.Serialize(instance, unionType);

        Assert.Contains($"\"kind\":\"{variantName}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"value\"", json, StringComparison.Ordinal);
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize(json, unionType)!;
        Assert.Equal(GetKindValue(unionType, variantName), ReadKind(roundTripped));
        var actual = payloadAccessor is null
            ? ReadProperty(roundTripped, propertyName)
            : ReadProperty(ReadPayload(roundTripped, payloadAccessor), propertyName);
        Assert.Equal(expected, actual);
    }

    private static void AssertNewtonsoftJsonRoundTrip(
        Type unionType,
        string variantName,
        string propertyName,
        object argument,
        object expected,
        string? payloadAccessor = null)
    {
        var instance = InvokeFactory(unionType, variantName, argument);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(instance);

        Assert.Contains($"\"kind\":\"{variantName}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"value\"", json, StringComparison.Ordinal);
        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject(json, unionType)!;
        Assert.Equal(GetKindValue(unionType, variantName), ReadKind(roundTripped));
        var actual = payloadAccessor is null
            ? ReadProperty(roundTripped, propertyName)
            : ReadProperty(ReadPayload(roundTripped, payloadAccessor), propertyName);
        Assert.Equal(expected, actual);
    }

    private static string IncompatibleFieldsSource(string strategy) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct FieldCollisionUnion
        {
            [Variant] public static partial FieldCollisionUnion Count(int value);
            [Variant] public static partial FieldCollisionUnion Text(string value);
            [Variant] public static partial FieldCollisionUnion Tagged(bool tagged);
        }
        """;

    private static string ThreeWayCollisionSource(string strategy) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct ThreeWayCollisionUnion
        {
            [Variant] public static partial ThreeWayCollisionUnion HTTPResponse(int value);
            [Variant] public static partial ThreeWayCollisionUnion URL(string value);
            [Variant] public static partial ThreeWayCollisionUnion JsonValue(double value);
            [Variant] public static partial ThreeWayCollisionUnion Ready(bool ready);
        }
        """;

    private static string KeywordCollisionSource(string strategy) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct KeywordCollisionUnion
        {
            [Variant] public static partial KeywordCollisionUnion @class(int value);
            [Variant] public static partial KeywordCollisionUnion @event(string value);
        }
        """;

    private static string KeywordFieldSource(string strategy) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct KeywordFieldUnion
        {
            [Variant] public static partial KeywordFieldUnion First(int @event);
            [Variant] public static partial KeywordFieldUnion Second(string @class);
            [Variant] public static partial KeywordFieldUnion Third(bool regular);
        }
        """;

    private static string JsonCollisionSource(string strategy, string jsonLibrary) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion(json: JsonLibrary.{{jsonLibrary}}, ReferenceStorage = ReferenceStorage.Auto)]
        public partial struct JsonCollisionUnion
        {
            [Variant] public static partial JsonCollisionUnion Count(int value);
            [Variant] public static partial JsonCollisionUnion Text(string value);
            [Variant] public static partial JsonCollisionUnion @event(bool value);
        }
        """;

    private static string ToStringCollisionSource(string strategy) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct ToStringCollisionUnion
        {
            [Variant] public static partial ToStringCollisionUnion Count(int displayName);
            [Variant] public static partial ToStringCollisionUnion Text(string displayName);
        }
        """;

    private static string NullableReferenceSource(string strategy, bool nullableFirst)
    {
        var firstType = nullableFirst ? "Effect?" : "Effect";
        var secondType = nullableFirst ? "Effect" : "Effect?";

        return $$"""
            #nullable enable
            using Houtamelo.Spire;

            public sealed class Effect { }

            [DiscriminatedUnion({{strategy}})]
            public partial struct NullableReferenceUnion
            {
                [Variant] public static partial NullableReferenceUnion First({{firstType}} effect);
                [Variant] public static partial NullableReferenceUnion Second({{secondType}} effect);
            }
            """;
    }

    private static string NestedNullableReferenceSource(string strategy, bool nullableFirst)
    {
        var firstType = nullableFirst ? "List<Effect?>?" : "List<Effect>?";
        var secondType = nullableFirst ? "List<Effect>?" : "List<Effect?>?";

        return $$"""
            #nullable enable
            using System.Collections.Generic;
            using Houtamelo.Spire;

            public sealed class Effect { }

            [DiscriminatedUnion({{strategy}})]
            public partial struct NestedNullableReferenceUnion
            {
                [Variant] public static partial NestedNullableReferenceUnion First({{firstType}} effects);
                [Variant] public static partial NestedNullableReferenceUnion Second({{secondType}} effects);
            }
            """;
    }

    private static string NullableValueSource(string strategy) => $$"""
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct NullableValueUnion
        {
            [Variant] public static partial NullableValueUnion Integer(int value);
            [Variant] public static partial NullableValueUnion NullableInteger(int? value);
        }
        """;

    private static string TupleNullabilitySource(string strategy, bool firstNames) => $$"""
        #nullable enable
        using Houtamelo.Spire;

        public sealed class Effect { }

        [DiscriminatedUnion({{strategy}})]
        public partial struct TupleNullabilityUnion
        {
            [Variant] public static partial TupleNullabilityUnion First(
                (Effect? {{(firstNames ? "firstEffect" : "secondEffect")}},
                    int {{(firstNames ? "firstCount" : "secondCount")}}) value);
            [Variant] public static partial TupleNullabilityUnion Second(
                (Effect? {{(firstNames ? "secondEffect" : "firstEffect")}},
                    int {{(firstNames ? "secondCount" : "firstCount")}}) value);
        }
        """;

    private static string NestedGenericValueSource(string strategy) => $$"""
        #nullable enable
        using Houtamelo.Spire;

        public sealed class Effect { }

        public readonly struct Inner<T>
        {
            public readonly T Value;

            public Inner(T value) => Value = value;
        }

        public readonly struct Outer<T>
        {
            public readonly T Value;

            public Outer(T value) => Value = value;
        }

        [DiscriminatedUnion({{strategy}})]
        public partial struct NestedGenericValueUnion
        {
            [Variant] public static partial NestedGenericValueUnion First(
                Outer<Inner<Effect?>> value);
            [Variant] public static partial NestedGenericValueUnion Second(
                Outer<Inner<Effect>> value);
        }
        """;

    private static string NullableArraySource(string strategy) => $$"""
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct NullableArrayUnion
        {
            [Variant] public static partial NullableArrayUnion First(int?[] values);
            [Variant] public static partial NullableArrayUnion Second(int?[]? values);
        }
        """;

    private static string AcronymDigitSource(string strategy) => $$"""
        using Houtamelo.Spire;

        [DiscriminatedUnion({{strategy}})]
        public partial struct AcronymDigitUnion
        {
            [Variant] public static partial AcronymDigitUnion HTTP2Response(int value);
            [Variant] public static partial AcronymDigitUnion URL_V1(string value);
        }
        """;

    private static readonly string RecordFieldConflictSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial record RecordFieldConflictUnion
        {
            public partial record Count(int value) : RecordFieldConflictUnion;
            public partial record Text(string value) : RecordFieldConflictUnion;
        }
        """;

    private static readonly string OverloadedVariantSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct OverloadedVariantUnion
        {
            [Variant] public static partial OverloadedVariantUnion Choice(int value);
            [Variant] public static partial OverloadedVariantUnion Choice(string value);
        }
        """;

    private static readonly string AnalyzerSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct AnalyzerCollisionUnion
        {
            [Variant] public static partial AnalyzerCollisionUnion Count(int value);
            [Variant] public static partial AnalyzerCollisionUnion Text(string value);
        }

        public static class AnalyzerReader
        {
            public static string Read(AnalyzerCollisionUnion command)
            {
                switch (command.kind)
                {
                    case AnalyzerCollisionUnion.Kind.Count:
                        return command.count.value.ToString();
                    case AnalyzerCollisionUnion.Kind.Text:
                        return command.text.value;
                    default:
                        return string.Empty;
                }
            }

            public static string Match(AnalyzerCollisionUnion command) => command switch
            {
                { kind: AnalyzerCollisionUnion.Kind.Count, count: { value: var value } } => value.ToString(),
                { kind: AnalyzerCollisionUnion.Kind.Text, text: { value: var value } } => value,
                _ => string.Empty,
            };
        }
        """;

    private static readonly string WrongVariantAccessSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct WrongVariantCollisionUnion
        {
            [Variant] public static partial WrongVariantCollisionUnion Count(int value);
            [Variant] public static partial WrongVariantCollisionUnion Text(string value);
        }

        public static class WrongVariantReader
        {
            public static string Read(WrongVariantCollisionUnion command)
            {
                switch (command.kind)
                {
                    case WrongVariantCollisionUnion.Kind.Count:
                        return command.text.value;
                    default:
                        return string.Empty;
                }
            }
        }
        """;

    private static readonly string ExhaustivePropertyPatternSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct ExhaustiveCollisionUnion
        {
            [Variant] public static partial ExhaustiveCollisionUnion Count(int value);
            [Variant] public static partial ExhaustiveCollisionUnion Text(string value);
        }

        public static class ExhaustiveReader
        {
            public static string Match(ExhaustiveCollisionUnion command) => command switch
            {
                { kind: ExhaustiveCollisionUnion.Kind.Count, count: { value: var value } } => value.ToString(),
                { kind: ExhaustiveCollisionUnion.Kind.Text, text: { value: var value } } => value,
            };
        }
        """;

    private static readonly string MissingVariantPropertyPatternSource = """
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct MissingVariantCollisionUnion
        {
            [Variant] public static partial MissingVariantCollisionUnion Count(int value);
            [Variant] public static partial MissingVariantCollisionUnion Text(string value);
        }

        public static class MissingVariantReader
        {
            public static string Match(MissingVariantCollisionUnion command) => command switch
            {
                { kind: MissingVariantCollisionUnion.Kind.Count, count: { value: var value } } => value.ToString(),
            };
        }
        """;

    private static Compilation Generate(string source)
    {
        GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: "field-collision.cs");

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        return compilation;
    }

    private static INamedTypeSymbol GetUnion(Compilation compilation, string metadataName)
    {
        var union = compilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(union);
        return union!;
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        var compilation = Generate(source);
        var diagnostics = await compilation
            .WithAnalyzers(analyzers.ToImmutableArray())
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics;
    }
}

public sealed class UnityFieldCollisionTests
{
    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializablePayload_PreservesOriginalFactoryAndFieldSchema()
    {
        var result = UnityGeneratorTestHarness.Run("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct SerializableFieldCollisionUnion
            {
                [Variant] public static partial SerializableFieldCollisionUnion Count(int value);
                [Variant] public static partial SerializableFieldCollisionUnion Text(string value);
            }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = result.OutputCompilation
            .GetTypeByMetadataName("SerializableFieldCollisionUnion");
        Assert.NotNull(union);

        Assert.NotNull(union!.GetMembers("count").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("text").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.Empty(union.GetMembers("value").OfType<IPropertySymbol>());

        var countFactory = Assert.Single(union.GetMembers("Count").OfType<IMethodSymbol>());
        var textFactory = Assert.Single(union.GetMembers("Text").OfType<IMethodSymbol>());
        Assert.Equal("value", Assert.Single(countFactory.Parameters).Name);
        Assert.Equal("value", Assert.Single(textFactory.Parameters).Name);
        Assert.Equal(SpecialType.System_Int32, countFactory.Parameters[0].Type.SpecialType);
        Assert.Equal(SpecialType.System_String, textFactory.Parameters[0].Type.SpecialType);

        var countPayload = result.OutputCompilation
            .GetTypeByMetadataName("SerializableFieldCollisionUnionTypes+Variants+Count");
        var textPayload = result.OutputCompilation
            .GetTypeByMetadataName("SerializableFieldCollisionUnionTypes+Variants+Text");
        Assert.NotNull(countPayload);
        Assert.NotNull(textPayload);

        AssertPayloadFieldIsSerialized(countPayload!);
        AssertPayloadFieldIsSerialized(textPayload!);
        Assert.DoesNotContain(InstanceFields(countPayload), field =>
            field.Name is "count_value" or "text_value");
        Assert.DoesNotContain(InstanceFields(textPayload), field =>
            field.Name is "count_value" or "text_value");
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializablePayload_PreservesPerVariantReferenceNullability()
    {
        var result = UnityGeneratorTestHarness.Run("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            public sealed class Effect { }

            [Serializable]
            [DiscriminatedUnion]
            public partial struct SerializableNullableFieldCollisionUnion
            {
                [Variant] public static partial SerializableNullableFieldCollisionUnion Required(
                    Effect value);
                [Variant] public static partial SerializableNullableFieldCollisionUnion Optional(
                    Effect? value);
            }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = result.OutputCompilation
            .GetTypeByMetadataName("SerializableNullableFieldCollisionUnion");
        Assert.NotNull(union);

        var requiredFactory = Assert.Single(union!.GetMembers("Required").OfType<IMethodSymbol>());
        var optionalFactory = Assert.Single(union.GetMembers("Optional").OfType<IMethodSymbol>());
        Assert.Equal(NullableAnnotation.NotAnnotated, requiredFactory.Parameters[0].NullableAnnotation);
        Assert.Equal(NullableAnnotation.Annotated, optionalFactory.Parameters[0].NullableAnnotation);

        var requiredPayload = result.OutputCompilation
            .GetTypeByMetadataName("SerializableNullableFieldCollisionUnionTypes+Variants+Required");
        var optionalPayload = result.OutputCompilation
            .GetTypeByMetadataName("SerializableNullableFieldCollisionUnionTypes+Variants+Optional");
        Assert.NotNull(requiredPayload);
        Assert.NotNull(optionalPayload);

        AssertPayloadFieldIsSerialized(requiredPayload!);
        AssertPayloadFieldIsSerialized(optionalPayload!);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void SerializableKeywordFields_KeepEscapedRuntimeAndPayloadNames()
    {
        var result = UnityGeneratorTestHarness.Run("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [Serializable]
            [DiscriminatedUnion]
            public partial struct SerializableKeywordFieldUnion
            {
                [Variant] public static partial SerializableKeywordFieldUnion First(int @event);
                [Variant] public static partial SerializableKeywordFieldUnion Second(string @class);
            }
            """);

        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var union = result.OutputCompilation
            .GetTypeByMetadataName("SerializableKeywordFieldUnion");
        Assert.NotNull(union);
        Assert.NotNull(union!.GetMembers("first").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("second").OfType<IPropertySymbol>().SingleOrDefault());

        var eventPayload = result.OutputCompilation
            .GetTypeByMetadataName("SerializableKeywordFieldUnionTypes+Variants+First");
        var classPayload = result.OutputCompilation
            .GetTypeByMetadataName("SerializableKeywordFieldUnionTypes+Variants+Second");
        Assert.NotNull(eventPayload);
        Assert.NotNull(classPayload);
        AssertPayloadFieldIsSerialized(eventPayload!, "event");
        AssertPayloadFieldIsSerialized(classPayload!, "class");
    }

    private static IFieldSymbol[] InstanceFields(INamedTypeSymbol type)
        => type.GetMembers().OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic)
            .ToArray();

    private static void AssertPayloadFieldIsSerialized(
        INamedTypeSymbol payload,
        string fieldName = "value")
    {
        var field = Assert.Single(InstanceFields(payload), candidate => candidate.Name == "_" + fieldName);
        Assert.True(
            HasAttribute(field, "UnityEngine.SerializeField"),
            $"Payload field '{payload.Name}.{fieldName}' must retain SerializeField.");
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == metadataName);
}
