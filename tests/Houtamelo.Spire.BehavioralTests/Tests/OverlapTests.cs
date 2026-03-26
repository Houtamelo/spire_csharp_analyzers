using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class OverlapTests
{
    [Fact]
    public void Circle_TagAndRadius()
    {
        var c = ShapeOvl.Circle(3.14);
        Assert.Equal(ShapeOvl.Kind.Circle, c.kind);
        Assert.Equal(3.14, c.radius);
    }

    [Fact]
    public void Rectangle_AllFields()
    {
        var r = ShapeOvl.Rectangle(1.5f, 2.5f);
        Assert.Equal(ShapeOvl.Kind.Rectangle, r.kind);
        Assert.Equal(1.5f, r.width);
        Assert.Equal(2.5f, r.height);
    }

    [Fact]
    public void SwitchOnTag()
    {
        Assert.Equal("circle", Match(ShapeOvl.Circle(1)));
        Assert.Equal("square", Match(ShapeOvl.Square(1)));
        Assert.Equal("rectangle", Match(ShapeOvl.Rectangle(1, 2)));
        Assert.Equal("point", Match(ShapeOvl.Point()));

        static string Match(ShapeOvl s) => s.kind switch
        {
            ShapeOvl.Kind.Circle => "circle",
            ShapeOvl.Kind.Square => "square",
            ShapeOvl.Kind.Rectangle => "rectangle",
            ShapeOvl.Kind.Point => "point",
            _ => "unknown"
        };
    }

    [Fact]
    public void PropertyPattern_SingleField()
    {
        var c = ShapeOvl.Circle(7.5);
        var r = c switch
        {
            { kind: ShapeOvl.Kind.Circle, radius: var v } => v,
            _ => -1.0
        };
        Assert.Equal(7.5, r);
    }

    [Fact]
    public void PropertyPattern_MultiField()
    {
        var rect = ShapeOvl.Rectangle(3.0f, 4.0f);
        var (w, h) = rect switch
        {
            { kind: ShapeOvl.Kind.Rectangle, width: var ww, height: var hh } => (ww, hh),
            _ => (0f, 0f)
        };
        Assert.Equal(3.0f, w);
        Assert.Equal(4.0f, h);
    }

    [Fact]
    public void SharedFields_Vec()
    {
        var v2 = VecOvl.Vec2(1.0, 2.0);
        Assert.Equal(1.0, v2.x);
        Assert.Equal(2.0, v2.y);

        var v3 = VecOvl.Vec3(3.0, 4.0, 5.0);
        Assert.Equal(3.0, v3.x);
        Assert.Equal(4.0, v3.y);
        Assert.Equal(5.0, v3.z);
    }

    [Fact]
    public void RefFields()
    {
        var m = MsgOvl.Label("hello");
        Assert.Equal("hello", m.text);

        var line = MsgOvl.ColoredLine(10, 20, "red");
        Assert.Equal(10, line.x);
        Assert.Equal(20, line.y);
        Assert.Equal("red", line.color);
    }

    [Fact]
    public void JsonStj_SingleField_RoundTrip()
    {
        var c = JsonShapeOvlStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeOvlStj>(json);
        Assert.Equal(JsonShapeOvlStj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonStj_MultiField_RoundTrip()
    {
        var r = JsonShapeOvlStj.Rectangle(1.5f, 2.5f);
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeOvlStj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonStj_Fieldless_RoundTrip()
    {
        var p = JsonShapeOvlStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeOvlStj>(json);
        Assert.Equal(JsonShapeOvlStj.Kind.Point, d.kind);
    }

    [Fact]
    public void JsonNsj_SingleField_RoundTrip()
    {
        var c = JsonShapeOvlNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonShapeOvlNsj>(json);
        Assert.Equal(JsonShapeOvlNsj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonNsj_MultiField_RoundTrip()
    {
        var r = JsonShapeOvlNsj.Rectangle(1.5f, 2.5f);
        var json = JsonConvert.SerializeObject(r);
        var d = JsonConvert.DeserializeObject<JsonShapeOvlNsj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonNsj_Fieldless_RoundTrip()
    {
        var p = JsonShapeOvlNsj.Point();
        var json = JsonConvert.SerializeObject(p);
        var d = JsonConvert.DeserializeObject<JsonShapeOvlNsj>(json);
        Assert.Equal(JsonShapeOvlNsj.Kind.Point, d.kind);
    }
}
