using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class UnsafeOverlapTests
{
    [Fact]
    public void Circle_TagAndRadius()
    {
        var c = ShapeUo.Circle(3.14);
        Assert.Equal(ShapeUo.Kind.Circle, c.kind);
        Assert.Equal(3.14, c.radius);
    }

    [Fact]
    public void Square_TagAndSideLength()
    {
        var s = ShapeUo.Square(42);
        Assert.Equal(ShapeUo.Kind.Square, s.kind);
        Assert.Equal(42, s.sideLength);
    }

    [Fact]
    public void Rectangle_AllFields()
    {
        var r = ShapeUo.Rectangle(1.5f, 2.5f);
        Assert.Equal(ShapeUo.Kind.Rectangle, r.kind);
        Assert.Equal(1.5f, r.width);
        Assert.Equal(2.5f, r.height);
    }

    [Fact]
    public void SwitchOnTag()
    {
        Assert.Equal("circle", Match(ShapeUo.Circle(1)));
        Assert.Equal("point", Match(ShapeUo.Point()));

        static string Match(ShapeUo s) => s.kind switch
        {
            ShapeUo.Kind.Circle => "circle",
            ShapeUo.Kind.Square => "square",
            ShapeUo.Kind.Rectangle => "rectangle",
            ShapeUo.Kind.Point => "point",
            _ => "unknown"
        };
    }

    [Fact]
    public void PropertyPattern()
    {
        var c = ShapeUo.Circle(7.5);
        var r = c switch
        {
            { kind: ShapeUo.Kind.Circle, radius: var v } => v,
            _ => -1.0
        };
        Assert.Equal(7.5, r);
    }

    [Fact]
    public void PropertyPattern_MultiField()
    {
        var rect = ShapeUo.Rectangle(3.0f, 4.0f);
        var (w, h) = rect switch
        {
            { kind: ShapeUo.Kind.Rectangle, width: var ww, height: var hh } => (ww, hh),
            _ => (0f, 0f)
        };
        Assert.Equal(3.0f, w);
        Assert.Equal(4.0f, h);
    }

    [Fact]
    public void SharedFields()
    {
        var v2 = VecUo.Vec2(1.0, 2.0);
        Assert.Equal(1.0, v2.x);
        Assert.Equal(2.0, v2.y);

        var v3 = VecUo.Vec3(3.0, 4.0, 5.0);
        Assert.Equal(3.0, v3.x);
        Assert.Equal(4.0, v3.y);
        Assert.Equal(5.0, v3.z);
    }

    [Fact]
    public void RefFields()
    {
        var m = MsgUo.Label("hello");
        Assert.Equal("hello", m.text);

        var line = MsgUo.ColoredLine(10, 20, "red");
        Assert.Equal(10, line.x);
        Assert.Equal(20, line.y);
        Assert.Equal("red", line.color);
    }

    [Fact]
    public void JsonStj_SingleField_RoundTrip()
    {
        var c = JsonShapeUoStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeUoStj>(json);
        Assert.Equal(JsonShapeUoStj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonStj_MultiField_RoundTrip()
    {
        var r = JsonShapeUoStj.Rectangle(1.5f, 2.5f);
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeUoStj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonStj_Fieldless_RoundTrip()
    {
        var p = JsonShapeUoStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeUoStj>(json);
        Assert.Equal(JsonShapeUoStj.Kind.Point, d.kind);
    }

    [Fact]
    public void JsonNsj_SingleField_RoundTrip()
    {
        var c = JsonShapeUoNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonShapeUoNsj>(json);
        Assert.Equal(JsonShapeUoNsj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonNsj_MultiField_RoundTrip()
    {
        var r = JsonShapeUoNsj.Rectangle(1.5f, 2.5f);
        var json = JsonConvert.SerializeObject(r);
        var d = JsonConvert.DeserializeObject<JsonShapeUoNsj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonNsj_Fieldless_RoundTrip()
    {
        var p = JsonShapeUoNsj.Point();
        var json = JsonConvert.SerializeObject(p);
        var d = JsonConvert.DeserializeObject<JsonShapeUoNsj>(json);
        Assert.Equal(JsonShapeUoNsj.Kind.Point, d.kind);
    }
}
