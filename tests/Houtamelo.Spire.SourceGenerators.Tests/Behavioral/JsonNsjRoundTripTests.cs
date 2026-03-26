using System;
using Newtonsoft.Json;
using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class JsonNsjRoundTripTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void SingleField_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonNsjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);

        var json = JsonConvert.SerializeObject(circle);
        var deserialized = JsonConvert.DeserializeObject(json, shapeType)!;

        Assert.Equal(GetKindValue(shapeType, "Circle"), ReadKind(deserialized));
        Assert.Equal(3.14, ReadProperty(deserialized, "radius"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void MultiField_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonNsjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var rect = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);

        var json = JsonConvert.SerializeObject(rect);
        var deserialized = JsonConvert.DeserializeObject(json, shapeType)!;

        Assert.Equal(GetKindValue(shapeType, "Rectangle"), ReadKind(deserialized));
        Assert.Equal(1.5f, ReadProperty(deserialized, "width"));
        Assert.Equal(2.5f, ReadProperty(deserialized, "height"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Fieldless_RoundTrip(string strategy)
    {
        var asm = CompileAndLoad(JsonNsjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var point = InvokeFactory(shapeType, "Point");

        var json = JsonConvert.SerializeObject(point);
        var deserialized = JsonConvert.DeserializeObject(json, shapeType)!;

        Assert.Equal(GetKindValue(shapeType, "Point"), ReadKind(deserialized));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Discriminator_PresentInJson(string strategy)
    {
        var asm = CompileAndLoad(JsonNsjSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);

        var json = JsonConvert.SerializeObject(circle);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }

    // ── Record JSON ─────────────────────────────────────────────

    [Fact]
    public void Record_SingleField_RoundTrip()
    {
        var asm = CompileAndLoad(JsonNsjRecordSource);
        var shapeType = GetType(asm, "TestNs.Shape");
        var circleType = GetType(asm, "TestNs.Shape+Circle");

        var circle = Activator.CreateInstance(circleType, 3.14)!;
        var json = JsonConvert.SerializeObject(circle);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);

        var deserialized = JsonConvert.DeserializeObject(json, shapeType)!;
        Assert.Equal(circleType, deserialized.GetType());
        Assert.Equal(3.14, ReadProperty(deserialized, "Radius"));
    }

    [Fact]
    public void Record_Fieldless_RoundTrip()
    {
        var asm = CompileAndLoad(JsonNsjRecordSource);
        var shapeType = GetType(asm, "TestNs.Shape");
        var pointType = GetType(asm, "TestNs.Shape+Point");

        var point = Activator.CreateInstance(pointType)!;
        var json = JsonConvert.SerializeObject(point);
        var deserialized = JsonConvert.DeserializeObject(json, shapeType)!;
        Assert.Equal(pointType, deserialized.GetType());
    }

    // ── Error cases ─────────────────────────────────────────────

    [Fact]
    public void MissingDiscriminator_Throws()
    {
        var asm = CompileAndLoad(JsonNsjSource("Layout.Additive"));
        var shapeType = GetType(asm, "Shape");
        Assert.ThrowsAny<Exception>(() =>
            JsonConvert.DeserializeObject("{\"radius\":3.14}", shapeType));
    }

    [Fact]
    public void UnknownKind_Throws()
    {
        var asm = CompileAndLoad(JsonNsjSource("Layout.Additive"));
        var shapeType = GetType(asm, "Shape");
        Assert.ThrowsAny<Exception>(() =>
            JsonConvert.DeserializeObject("{\"kind\":\"Triangle\"}", shapeType));
    }
}
