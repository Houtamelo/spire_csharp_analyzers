using System.Text.Json;
using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class JsonRecordTests
{
    // ── System.Text.Json ────────────────────────────────────────

    [Fact]
    public void Stj_SingleField_RoundTrip()
    {
        var c = new JsonShapeRecStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeRecStj>(json)!;
        var circle = Assert.IsType<JsonShapeRecStj.Circle>(d);
        Assert.Equal(3.14, circle.Radius);
    }

    [Fact]
    public void Stj_IntField_RoundTrip()
    {
        var s = new JsonShapeRecStj.Square(42);
        var json = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(s);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeRecStj>(json)!;
        var square = Assert.IsType<JsonShapeRecStj.Square>(d);
        Assert.Equal(42, square.Side);
    }

    [Fact]
    public void Stj_Fieldless_RoundTrip()
    {
        var p = new JsonShapeRecStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeRecStj>(json)!;
        Assert.IsType<JsonShapeRecStj.Point>(d);
    }

    [Fact]
    public void Stj_Discriminator_Present()
    {
        var c = new JsonShapeRecStj.Circle(1.0);
        var json = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(c);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }

    [Fact]
    public void Stj_AllVariants_Discriminators()
    {
        var json1 = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(new JsonShapeRecStj.Circle(1));
        Assert.Contains("\"Circle\"", json1);

        var json2 = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(new JsonShapeRecStj.Square(1));
        Assert.Contains("\"Square\"", json2);

        var json3 = System.Text.Json.JsonSerializer.Serialize<JsonShapeRecStj>(new JsonShapeRecStj.Point());
        Assert.Contains("\"Point\"", json3);
    }

    // ── Newtonsoft.Json ─────────────────────────────────────────

    [Fact]
    public void Nsj_SingleField_RoundTrip()
    {
        JsonShapeRecNsj c = new JsonShapeRecNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonShapeRecNsj>(json)!;
        var circle = Assert.IsType<JsonShapeRecNsj.Circle>(d);
        Assert.Equal(3.14, circle.Radius);
    }

    [Fact]
    public void Nsj_Fieldless_RoundTrip()
    {
        JsonShapeRecNsj p = new JsonShapeRecNsj.Point();
        var json = JsonConvert.SerializeObject(p);
        var d = JsonConvert.DeserializeObject<JsonShapeRecNsj>(json)!;
        Assert.IsType<JsonShapeRecNsj.Point>(d);
    }

    [Fact]
    public void Nsj_Discriminator_Present()
    {
        JsonShapeRecNsj c = new JsonShapeRecNsj.Circle(1.0);
        var json = JsonConvert.SerializeObject(c);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }
}
