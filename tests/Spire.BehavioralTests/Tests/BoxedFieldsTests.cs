using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class BoxedFieldsTests
{
    [Fact]
    public void Circle_TagAndRadius()
    {
        var c = ShapeBf.Circle(3.14);
        Assert.Equal(ShapeBf.Kind.Circle, c.kind);
        Assert.Equal(3.14, c.radius);
    }

    [Fact]
    public void Rectangle_AllFields()
    {
        var r = ShapeBf.Rectangle(1.5f, 2.5f);
        Assert.Equal(ShapeBf.Kind.Rectangle, r.kind);
        Assert.Equal(1.5f, r.width);
        Assert.Equal(2.5f, r.height);
    }

    [Fact]
    public void SwitchOnTag()
    {
        Assert.Equal("circle", Match(ShapeBf.Circle(1)));
        Assert.Equal("point", Match(ShapeBf.Point()));

        static string Match(ShapeBf s) => s.kind switch
        {
            ShapeBf.Kind.Circle => "circle",
            ShapeBf.Kind.Square => "square",
            ShapeBf.Kind.Rectangle => "rectangle",
            ShapeBf.Kind.Point => "point",
            _ => "unknown"
        };
    }

    [Fact]
    public void PropertyPattern()
    {
        var c = ShapeBf.Circle(7.5);
        var r = c switch
        {
            { kind: ShapeBf.Kind.Circle, radius: var v } => v,
            _ => -1.0
        };
        Assert.Equal(7.5, r);
    }

    [Fact]
    public void SharedFields()
    {
        var v2 = VecBf.Vec2(1.0, 2.0);
        Assert.Equal(1.0, v2.x);
        Assert.Equal(2.0, v2.y);

        var v3 = VecBf.Vec3(3.0, 4.0, 5.0);
        Assert.Equal(3.0, v3.x);
        Assert.Equal(4.0, v3.y);
        Assert.Equal(5.0, v3.z);
    }

    [Fact]
    public void Generic_Option()
    {
        var o = OptionBf<int>.Some(42);
        Assert.Equal(42, o.value);
        Assert.Equal(OptionBf<int>.Kind.Some, o.kind);

        var n = OptionBf<string>.None();
        Assert.Equal(OptionBf<string>.Kind.None, n.kind);
    }

    [Fact]
    public void RefFields()
    {
        var m = MsgBf.Label("hello");
        Assert.Equal("hello", m.text);

        var line = MsgBf.ColoredLine(10, 20, "red");
        Assert.Equal(10, line.x);
        Assert.Equal("red", line.color);
    }

    [Fact]
    public void JsonStj_SingleField_RoundTrip()
    {
        var c = JsonShapeBfStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeBfStj>(json);
        Assert.Equal(JsonShapeBfStj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonStj_MultiField_RoundTrip()
    {
        var r = JsonShapeBfStj.Rectangle(1.5f, 2.5f);
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeBfStj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonStj_Fieldless_RoundTrip()
    {
        var p = JsonShapeBfStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeBfStj>(json);
        Assert.Equal(JsonShapeBfStj.Kind.Point, d.kind);
    }

    [Fact]
    public void JsonNsj_SingleField_RoundTrip()
    {
        var c = JsonShapeBfNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonShapeBfNsj>(json);
        Assert.Equal(JsonShapeBfNsj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonNsj_MultiField_RoundTrip()
    {
        var r = JsonShapeBfNsj.Rectangle(1.5f, 2.5f);
        var json = JsonConvert.SerializeObject(r);
        var d = JsonConvert.DeserializeObject<JsonShapeBfNsj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonNsj_Fieldless_RoundTrip()
    {
        var p = JsonShapeBfNsj.Point();
        var json = JsonConvert.SerializeObject(p);
        var d = JsonConvert.DeserializeObject<JsonShapeBfNsj>(json);
        Assert.Equal(JsonShapeBfNsj.Kind.Point, d.kind);
    }
}
