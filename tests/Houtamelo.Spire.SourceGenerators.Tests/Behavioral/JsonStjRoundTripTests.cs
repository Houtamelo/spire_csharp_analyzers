using System;
using System.Text.Json;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public class JsonStjRoundTripTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void SingleField_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonStjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);

        var json = JsonSerializer.Serialize(circle, shapeType);
        var deserialized = JsonSerializer.Deserialize(json, shapeType)!;

        Assert.Equal(GetKindValue(shapeType, "Circle"), ReadKind(deserialized));
        Assert.Equal(3.14, ReadProperty(deserialized, "radius"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void MultiField_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonStjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var rect = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);

        var json = JsonSerializer.Serialize(rect, shapeType);
        var deserialized = JsonSerializer.Deserialize(json, shapeType)!;

        Assert.Equal(GetKindValue(shapeType, "Rectangle"), ReadKind(deserialized));
        Assert.Equal(1.5f, ReadProperty(deserialized, "width"));
        Assert.Equal(2.5f, ReadProperty(deserialized, "height"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Fieldless_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonStjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var point = InvokeFactory(shapeType, "Point");

        var json = JsonSerializer.Serialize(point, shapeType);
        var deserialized = JsonSerializer.Deserialize(json, shapeType)!;

        Assert.Equal(GetKindValue(shapeType, "Point"), ReadKind(deserialized));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Discriminator_PresentInJson(string strategy)
    {
        var asm = CompileAndLoad(JsonStjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);

        var json = JsonSerializer.Serialize(circle, shapeType);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void CustomDiscriminator(string strategy)
    {
        var asm = CompileAndLoad(JsonStjCustomDiscriminatorSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);

        var json = JsonSerializer.Serialize(circle, shapeType);
        Assert.Contains("\"type\"", json);
        Assert.Contains("\"Circle\"", json);
        // Verify "kind" is NOT present
        Assert.DoesNotContain("\"kind\"", json);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void JsonName_VariantOverride(string strategy)
    {
        var asm = CompileAndLoad(JsonStjJsonNameSource(strategy));
        var shapeType = GetType(asm, "Shape");

        // [JsonName("circle")] on Circle variant
        var circle = InvokeFactory(shapeType, "Circle", 3.14);
        var json = JsonSerializer.Serialize(circle, shapeType);
        Assert.Contains("\"circle\"", json); // variant name override
        Assert.Contains("\"r\"", json);      // field name override

        // Round-trip
        var deserialized = JsonSerializer.Deserialize(json, shapeType)!;
        Assert.Equal(GetKindValue(shapeType, "Circle"), ReadKind(deserialized));
        Assert.Equal(3.14, ReadProperty(deserialized, "radius"));
    }

    // ── Record JSON ─────────────────────────────────────────────

    [Fact]
    public void Record_SingleField_RoundTrip()
    {
        var asm = CompileAndLoad(JsonStjRecordSource);
        var shapeType = GetType(asm, "TestNs.Shape");
        var circleType = GetType(asm, "TestNs.Shape+Circle");

        var circle = Activator.CreateInstance(circleType, 3.14)!;
        var json = JsonSerializer.Serialize(circle, shapeType);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);

        var deserialized = JsonSerializer.Deserialize(json, shapeType)!;
        Assert.Equal(circleType, deserialized.GetType());
        Assert.Equal(3.14, ReadProperty(deserialized, "Radius"));
    }

    [Fact]
    public void Record_Fieldless_RoundTrip()
    {
        var asm = CompileAndLoad(JsonStjRecordSource);
        var shapeType = GetType(asm, "TestNs.Shape");
        var pointType = GetType(asm, "TestNs.Shape+Point");

        var point = Activator.CreateInstance(pointType)!;
        var json = JsonSerializer.Serialize(point, shapeType);
        var deserialized = JsonSerializer.Deserialize(json, shapeType)!;
        Assert.Equal(pointType, deserialized.GetType());
    }

    // ── Generic JSON ────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GenericCapableStrategies))]
    public void Generic_IntSome_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonStjGenericSource(strategy));
        var openType = asm.GetType("Option`1")!;
        var optionInt = openType.MakeGenericType(typeof(int));

        var some = InvokeFactory(optionInt, "Some", 42);
        var json = JsonSerializer.Serialize(some, optionInt);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Some\"", json);

        var deserialized = JsonSerializer.Deserialize(json, optionInt)!;
        Assert.Equal(GetKindValue(optionInt, "Some"), ReadKind(deserialized));
        Assert.Equal(42, ReadProperty(deserialized, "value"));
    }

    [Theory]
    [MemberData(nameof(GenericCapableStrategies))]
    public void Generic_None_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonStjGenericSource(strategy));
        var openType = asm.GetType("Option`1")!;
        var optionInt = openType.MakeGenericType(typeof(int));

        var none = InvokeFactory(optionInt, "None");
        var json = JsonSerializer.Serialize(none, optionInt);
        var deserialized = JsonSerializer.Deserialize(json, optionInt)!;
        Assert.Equal(GetKindValue(optionInt, "None"), ReadKind(deserialized));
    }

    // ── Error cases ─────────────────────────────────────────────

    [Fact]
    public void MissingDiscriminator_Throws()
    {
        var asm = CompileAndLoad(JsonStjSource("Layout.Additive"));
        var shapeType = GetType(asm, "Shape");
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize("{\"radius\":3.14}", shapeType));
    }

    [Fact]
    public void UnknownKind_Throws()
    {
        var asm = CompileAndLoad(JsonStjSource("Layout.Additive"));
        var shapeType = GetType(asm, "Shape");
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize("{\"kind\":\"Triangle\"}", shapeType));
    }
}
