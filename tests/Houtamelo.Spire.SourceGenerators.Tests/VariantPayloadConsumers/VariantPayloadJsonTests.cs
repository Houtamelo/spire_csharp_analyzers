using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.GlobalConfig;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.VariantPayloadConsumers;

/// Exercises the existing JSON contract against the new variant-payload API.
/// Each successful case compiles and loads the generator output before it uses
/// the actual generated converter.
public sealed class VariantPayloadJsonTests : Behavioral.BehavioralTestBase
{
    [Fact]
    public void Stj_FieldedPayloadRoundTripKeepsFlatWireFormat()
    {
        var assembly = CompileAndLoad(StjShapeSource());
        var unionType = GetType(assembly, "JsonShape");
        var value = InvokeFactory(unionType, "Move", 12, 1.5f);

        var json = JsonSerializer.Serialize(value, unionType);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Move", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(12, document.RootElement.GetProperty("destination").GetInt32());
        Assert.Equal(1.5f, document.RootElement.GetProperty("speed").GetSingle());
        Assert.False(document.RootElement.TryGetProperty("move", out _));

        var restored = JsonSerializer.Deserialize(json, unionType)!;
        Assert.Equal(GetKindValue(unionType, "Move"), ReadKind(restored));
        Assert.Equal(12, ReadPayloadField(restored, "move", "destination"));
        Assert.Equal(1.5f, ReadPayloadField(restored, "move", "speed"));
    }

    [Fact]
    public void Stj_FieldlessPayloadRoundTripKeepsDiscriminator()
    {
        var assembly = CompileAndLoad(StjShapeSource());
        var unionType = GetType(assembly, "JsonShape");
        var value = InvokeFactory(unionType, "Stop");

        var json = JsonSerializer.Serialize(value, unionType);
        Assert.Equal("Stop", JsonDocument.Parse(json).RootElement.GetProperty("kind").GetString());

        var restored = JsonSerializer.Deserialize(json, unionType)!;
        Assert.Equal(GetKindValue(unionType, "Stop"), ReadKind(restored));
        Assert.Equal(unionType.Assembly.GetType("JsonShapeTypes+Variants+Stop"),
            unionType.GetProperty("stop")!.PropertyType);
    }

    [Fact]
    public void Stj_CustomDiscriminatorAndJsonNamesRemainStable()
    {
        var assembly = CompileAndLoad(StjJsonNameSource());
        var unionType = GetType(assembly, "JsonNamed");
        var value = InvokeFactory(unionType, "Move", 7, 2.25f);

        var json = JsonSerializer.Serialize(value, unionType);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("move", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(7, document.RootElement.GetProperty("destination_value").GetInt32());
        Assert.Equal(2.25f, document.RootElement.GetProperty("velocity").GetSingle());
        Assert.False(document.RootElement.TryGetProperty("kind", out _));

        var restored = JsonSerializer.Deserialize(json, unionType)!;
        Assert.Equal(7, ReadPayloadField(restored, "move", "destination"));
        Assert.Equal(2.25f, ReadPayloadField(restored, "move", "speed"));
    }

    [Fact]
    public void Stj_SharedReferenceStorageRoundTripsManagedStructAndReferences()
    {
        var assembly = CompileAndLoad(StjSharedStorageSource());
        var unionType = GetType(assembly, "JsonShared");
        var pair = CreateManagedPair(assembly, "pair", 4);
        var value = InvokeFactory(unionType, "Pair", pair);

        var json = JsonSerializer.Serialize(value, unionType);
        Assert.Contains("\"pair\"", json, StringComparison.Ordinal);
        var restored = JsonSerializer.Deserialize(json, unionType)!;
        var restoredPair = ReadPayloadField(restored, "pair", "pair")!;
        Assert.Equal("pair", restoredPair.GetType().GetField("label")!.GetValue(restoredPair));
        Assert.Equal(4, restoredPair.GetType().GetField("count")!.GetValue(restoredPair));

        var values = new[] { 1, 2, 3 };
        var arrayValue = InvokeFactory(unionType, "Values", values);
        var arrayRestored = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(arrayValue, unionType), unionType)!;
        Assert.Equal(values, (int[])ReadPayloadField(arrayRestored, "values", "values")!);
    }

    [Fact]
    public void Stj_NestedUserStructHonorsCallerIncludeFieldsOption()
    {
        var assembly = CompileAndLoad(StjNestedFieldsSource());
        var unionType = GetType(assembly, "JsonNestedFields");
        var value = InvokeFactory(unionType, "Pair", CreateManagedPair(assembly, "nested", 9));

        var defaultJson = JsonSerializer.Serialize(value, unionType);
        using (var defaultDocument = JsonDocument.Parse(defaultJson))
        {
            var pair = defaultDocument.RootElement.GetProperty("pair");
            Assert.False(pair.TryGetProperty("label", out _));
            Assert.False(pair.TryGetProperty("count", out _));
        }

        var options = new JsonSerializerOptions { IncludeFields = true };
        var configuredJson = JsonSerializer.Serialize(value, unionType, options);
        using (var configuredDocument = JsonDocument.Parse(configuredJson))
        {
            var pair = configuredDocument.RootElement.GetProperty("pair");
            Assert.Equal("nested", pair.GetProperty("label").GetString());
            Assert.Equal(9, pair.GetProperty("count").GetInt32());
        }

        var restored = JsonSerializer.Deserialize(configuredJson, unionType, options)!;
        var restoredPair = ReadPayloadField(restored, "pair", "pair")!;
        Assert.Equal("nested", restoredPair.GetType().GetField("label")!.GetValue(restoredPair));
        Assert.Equal(9, restoredPair.GetType().GetField("count")!.GetValue(restoredPair));
    }

    [Fact]
    public void Stj_TypedAndAutoReferenceStoragePreservePayloadValues()
    {
        var assembly = CompileAndLoad(StjStorageModesSource());
        foreach (var unionName in new[] { "JsonTyped", "JsonAuto" })
        {
            var unionType = GetType(assembly, unionName);
            var value = InvokeFactory(unionType, "Name", "hello");
            var restored = JsonSerializer.Deserialize(
                JsonSerializer.Serialize(value, unionType), unionType)!;
            Assert.Equal("hello", ReadPayloadField(restored, "name", "name"));
        }
    }

    [Fact]
    public void Stj_GenericPayloadRoundTripClosesConverter()
    {
        var assembly = CompileAndLoad(StjGenericSource());
        var openType = GetType(assembly, "JsonOption`1");
        var unionType = openType.MakeGenericType(typeof(int));
        var value = InvokeFactory(unionType, "Some", 42);

        var json = JsonSerializer.Serialize(value, unionType);
        Assert.Equal("Some", JsonDocument.Parse(json).RootElement.GetProperty("kind").GetString());
        var restored = JsonSerializer.Deserialize(json, unionType)!;
        Assert.Equal(42, ReadPayloadField(restored, "some", "value"));
    }

    [Fact]
    public void Stj_NullableFieldAndCollectionRoundTrip()
    {
        var assembly = CompileAndLoad(StjNullableSource());
        var unionType = GetType(assembly, "JsonNullable");
        var value = InvokeFactoryAllowingNull(
            unionType, "Data", null, new List<int> { 3, 5 });

        var restored = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(value, unionType), unionType)!;
        Assert.Null(ReadPayloadField(restored, "data", "name"));
        Assert.Equal(new[] { 3, 5 },
            (IEnumerable<int>)ReadPayloadField(restored, "data", "values")!);
    }

    [Fact]
    public void Stj_UnknownAndMissingDiscriminatorThrowJsonException()
    {
        var assembly = CompileAndLoad(StjShapeSource());
        var unionType = GetType(assembly, "JsonShape");
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize("{\"kind\":\"Unknown\"}", unionType));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize("{\"destination\":1}", unionType));
    }

    [Fact]
    public void Nsj_FieldedAndFieldlessPayloadsRoundTrip()
    {
        var assembly = CompileAndLoad(NsjShapeSource());
        var unionType = GetType(assembly, "NewtonsoftShape");
        var moved = InvokeFactory(unionType, "Move", 4, 9.5f);
        var movedJson = JsonConvert.SerializeObject(moved);
        var movedRoot = Newtonsoft.Json.Linq.JObject.Parse(movedJson);
        Assert.Equal("Move", (string?)movedRoot["kind"]);
        Assert.Equal(4, (int?)movedRoot["destination"]);
        Assert.Equal(9.5f, (float?)movedRoot["speed"]);
        var restored = JsonConvert.DeserializeObject(movedJson, unionType)!;
        Assert.Equal(4, ReadPayloadField(restored, "move", "destination"));

        var stop = InvokeFactory(unionType, "Stop");
        var stopJson = JsonConvert.SerializeObject(stop);
        Assert.Equal("Stop", (string?)Newtonsoft.Json.Linq.JObject.Parse(stopJson)["kind"]);
    }

    [Fact]
    public void Nsj_CustomDiscriminatorAndJsonNamesRoundTrip()
    {
        var assembly = CompileAndLoad(NsjJsonNameSource());
        var unionType = GetType(assembly, "NewtonsoftNamed");
        var value = InvokeFactory(unionType, "Move", 8, 6.5f);
        var json = JsonConvert.SerializeObject(value);
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        Assert.Equal("move", (string?)root["type"]);
        Assert.Equal(8, (int?)root["destination_value"]);
        Assert.Equal(6.5f, (float?)root["velocity"]);
        Assert.Null(root["kind"]);
        var restored = JsonConvert.DeserializeObject(json, unionType)!;
        Assert.Equal(8, ReadPayloadField(restored, "move", "destination"));
    }

    [Fact]
    public void Nsj_SharedManagedStructRoundTrip()
    {
        var assembly = CompileAndLoad(NsjSharedStorageSource());
        var unionType = GetType(assembly, "NewtonsoftShared");
        var value = InvokeFactory(unionType, "Pair", CreateManagedPair(assembly, "nsj", 6));
        var restored = JsonConvert.DeserializeObject(
            JsonConvert.SerializeObject(value), unionType)!;
        var pair = ReadPayloadField(restored, "pair", "pair")!;
        Assert.Equal("nsj", pair.GetType().GetField("label")!.GetValue(pair));
        Assert.Equal(6, pair.GetType().GetField("count")!.GetValue(pair));
    }

    [Fact]
    public void Nsj_GenericPayloadRoundTripClosesConverter()
    {
        var assembly = CompileAndLoad(NsjGenericSource());
        var unionType = GetType(assembly, "NewtonsoftOption`1").MakeGenericType(typeof(int));
        var value = InvokeFactory(unionType, "Some", 19);
        var restored = JsonConvert.DeserializeObject(
            JsonConvert.SerializeObject(value), unionType)!;
        Assert.Equal(19, ReadPayloadField(restored, "some", "value"));
    }

    [Fact]
    public void Nsj_UnknownAndMissingDiscriminatorThrow()
    {
        var assembly = CompileAndLoad(NsjShapeSource());
        var unionType = GetType(assembly, "NewtonsoftShape");
        Assert.ThrowsAny<Exception>(() =>
            JsonConvert.DeserializeObject("{\"kind\":\"Unknown\"}", unionType));
        Assert.ThrowsAny<Exception>(() =>
            JsonConvert.DeserializeObject("{\"destination\":1}", unionType));
    }

    [Fact]
    public void Nsj_RecordControlRemainsUnchanged()
    {
        var assembly = CompileAndLoad(NsjRecordSource());
        var unionType = GetType(assembly, "JsonRecords.Shape");
        var circleType = GetType(assembly, "JsonRecords.Shape+Circle");
        var circle = Activator.CreateInstance(circleType, 3.25)!;
        var json = JsonConvert.SerializeObject(circle);
        Assert.Equal("Circle", (string?)Newtonsoft.Json.Linq.JObject.Parse(json)["kind"]);
        var restored = JsonConvert.DeserializeObject(json, unionType)!;
        Assert.Equal(circleType, restored.GetType());
    }

    [Fact]
    public void GlobalJsonDefaultAndExplicitNoneControlConverterEmission()
    {
        var inherited = GenerateWithConfig(
            ConfigUnionSource(),
            new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
            });
        Assert.Contains(inherited.Result.GeneratedTrees,
            tree => tree.FilePath.EndsWith(".Stj.g.cs", StringComparison.Ordinal));

        var disabled = GenerateWithConfig(
            ConfigUnionSource("JsonLibrary.None"),
            new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
            });
        Assert.DoesNotContain(disabled.Result.GeneratedTrees,
            tree => tree.FilePath.EndsWith(".Stj.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void GlobalJsonReadGlobalCfgExplicitlyInheritsDefault()
    {
        var result = GenerateWithConfig(
            ConfigUnionSource("JsonLibrary.ReadGlobalCfg"),
            new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultJson"] = "NewtonsoftJson",
            });
        Assert.Contains(result.Result.GeneratedTrees,
            tree => tree.FilePath.EndsWith(".Nsj.g.cs", StringComparison.Ordinal));
    }

    private static object? ReadPayloadField(object union, string accessor, string field)
    {
        var payload = union.GetType().GetProperty(accessor)!.GetValue(union);
        return payload?.GetType().GetProperty(field)!.GetValue(payload);
    }

    private static object CreateManagedPair(System.Reflection.Assembly assembly, string label, int count)
    {
        var type = assembly.GetType("ManagedPair", throwOnError: true)!;
        var value = Activator.CreateInstance(type)!;
        type.GetField("label")!.SetValue(value, label);
        type.GetField("count")!.SetValue(value, count);
        return value;
    }

    private static object InvokeFactoryAllowingNull(Type unionType, string name, params object?[] args)
        => unionType.GetMethod(name)!.Invoke(null, args)!;

    private static (Microsoft.CodeAnalysis.GeneratorDriverRunResult Result, Compilation Compilation)
        GenerateWithConfig(string source, Dictionary<string, string> options)
    {
        var references = TestHarnessEnvironment.RuntimeReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(JsonSerializer).Assembly.Location))
            .Distinct(MetadataReferenceComparer.Instance)
            .ToArray();
        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), path: "config-json.cs");
        var compilation = CSharpCompilation.Create(
            "ConfigJsonTests",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                optionsProvider: new TestAnalyzerConfigOptionsProvider(options))
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }

    private sealed class MetadataReferenceComparer : IEqualityComparer<MetadataReference>
    {
        public static readonly MetadataReferenceComparer Instance = new();
        public bool Equals(MetadataReference? left, MetadataReference? right)
            => string.Equals(left?.Display, right?.Display, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(MetadataReference reference)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(reference.Display ?? string.Empty);
    }

    private static string StjShapeSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial struct JsonShape
        {
            [Variant] public static partial JsonShape Move(int destination, float speed);
            [Variant] public static partial JsonShape Stop();
        }
        """;

    private static string NsjShapeSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
        public partial struct NewtonsoftShape
        {
            [Variant] public static partial NewtonsoftShape Move(int destination, float speed);
            [Variant] public static partial NewtonsoftShape Stop();
        }
        """;

    private static string StjJsonNameSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson, jsonDiscriminator: "type")]
        public partial struct JsonNamed
        {
            [Variant, JsonName("move")]
            public static partial JsonNamed Move([JsonName("destination_value")] int destination, [JsonName("velocity")] float speed);
        }
        """;

    private static string NsjJsonNameSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson, jsonDiscriminator: "type")]
        public partial struct NewtonsoftNamed
        {
            [Variant, JsonName("move")]
            public static partial NewtonsoftNamed Move([JsonName("destination_value")] int destination, [JsonName("velocity")] float speed);
        }
        """;

    private static string StjSharedStorageSource() => """
        using Houtamelo.Spire;
        using System.Text.Json.Serialization;
        public struct ManagedPair
        {
            [JsonInclude] public string? label;
            [JsonInclude] public int count;
        }
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson, ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct JsonShared
        {
            [Variant] public static partial JsonShared Pair(ManagedPair pair);
            [Variant] public static partial JsonShared Name(string? name);
            [Variant] public static partial JsonShared Values(int[] values);
        }
        """;

    private static string StjNestedFieldsSource() => """
        using Houtamelo.Spire;
        public struct ManagedPair { public string? label; public int count; }
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial struct JsonNestedFields
        {
            [Variant] public static partial JsonNestedFields Pair(ManagedPair pair);
        }
        """;

    private static string NsjSharedStorageSource() => """
        using Houtamelo.Spire;
        public struct ManagedPair { public string? label; public int count; }
        [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson, ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct NewtonsoftShared
        {
            [Variant] public static partial NewtonsoftShared Pair(ManagedPair pair);
            [Variant] public static partial NewtonsoftShared Name(string? name);
            [Variant] public static partial NewtonsoftShared Values(int[] values);
        }
        """;

    private static string StjStorageModesSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson, ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct JsonTyped
        {
            [Variant] public static partial JsonTyped Name(string name);
            [Variant] public static partial JsonTyped Values(int[] values);
        }
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson, ReferenceStorage = ReferenceStorage.Auto)]
        public partial struct JsonAuto
        {
            [Variant] public static partial JsonAuto Name(string name);
            [Variant] public static partial JsonAuto Values(int[] values);
            [Variant] public static partial JsonAuto Number(long value);
            [Variant] public static partial JsonAuto Empty();
        }
        """;

    private static string StjGenericSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial struct JsonOption<T> where T : unmanaged
        {
            [Variant] public static partial JsonOption<T> Some(T value);
            [Variant] public static partial JsonOption<T> None();
        }
        """;

    private static string NsjGenericSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
        public partial struct NewtonsoftOption<T> where T : unmanaged
        {
            [Variant] public static partial NewtonsoftOption<T> Some(T value);
            [Variant] public static partial NewtonsoftOption<T> None();
        }
        """;

    private static string StjNullableSource() => """
        using System.Collections.Generic;
        using Houtamelo.Spire;
        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial struct JsonNullable
        {
            [Variant] public static partial JsonNullable Data(string? name, List<int> values);
            [Variant] public static partial JsonNullable Empty();
        }
        """;

    private static string NsjRecordSource() => """
        using Houtamelo.Spire;
        namespace JsonRecords
        {
            [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
            public abstract partial record Shape
            {
                public sealed partial record Circle(double Radius) : Shape;
                public sealed partial record Point() : Shape;
            }
        }
        """;

    private static string ConfigUnionSource(string? json = null)
    {
        var attribute = json is null ? "[DiscriminatedUnion]" : $"[DiscriminatedUnion(json: {json})]";
        return $$"""
            using Houtamelo.Spire;
            {{attribute}}
            public partial struct ConfigUnion
            {
                [Variant] public static partial ConfigUnion Value(int value);
            }
            """;
    }
}
