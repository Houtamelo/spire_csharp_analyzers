using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class BoxedTupleTests
{
    [Fact]
    public void Circle_TagAndRadius()
    {
        var c = ShapeBt.Circle(3.14);
        Assert.Equal(ShapeBt.Kind.Circle, c.tag);
        Assert.Equal(3.14, c.radius);
    }

    [Fact]
    public void Rectangle_AllFields()
    {
        var r = ShapeBt.Rectangle(1.5f, 2.5f);
        Assert.Equal(ShapeBt.Kind.Rectangle, r.tag);
        Assert.Equal(1.5f, r.width);
        Assert.Equal(2.5f, r.height);
    }

    [Fact]
    public void SwitchOnTag()
    {
        Assert.Equal("circle", Match(ShapeBt.Circle(1)));
        Assert.Equal("point", Match(ShapeBt.Point()));

        static string Match(ShapeBt s) => s.tag switch
        {
            ShapeBt.Kind.Circle => "circle",
            ShapeBt.Kind.Square => "square",
            ShapeBt.Kind.Rectangle => "rectangle",
            ShapeBt.Kind.Point => "point",
            _ => "unknown"
        };
    }

    [Fact]
    public void PropertyPattern()
    {
        var c = ShapeBt.Circle(7.5);
        var r = c switch
        {
            { tag: ShapeBt.Kind.Circle, radius: var v } => v,
            _ => -1.0
        };
        Assert.Equal(7.5, r);
    }

    [Fact]
    public void PropertyPattern_MultiField()
    {
        var rect = ShapeBt.Rectangle(3.0f, 4.0f);
        var (w, h) = rect switch
        {
            { tag: ShapeBt.Kind.Rectangle, width: var ww, height: var hh } => (ww, hh),
            _ => (0f, 0f)
        };
        Assert.Equal(3.0f, w);
        Assert.Equal(4.0f, h);
    }

    [Fact]
    public void SharedFields()
    {
        var v2 = VecBt.Vec2(1.0, 2.0);
        Assert.Equal(1.0, v2.x);
        Assert.Equal(2.0, v2.y);

        var v3 = VecBt.Vec3(3.0, 4.0, 5.0);
        Assert.Equal(3.0, v3.x);
        Assert.Equal(4.0, v3.y);
        Assert.Equal(5.0, v3.z);
    }

    [Fact]
    public void Generic_Option()
    {
        var o = OptionBt<int>.Some(42);
        Assert.Equal(42, o.value);
        Assert.Equal(OptionBt<int>.Kind.Some, o.tag);
    }

    [Fact]
    public void RefFields()
    {
        var m = MsgBt.Label("hello");
        Assert.Equal("hello", m.text);
    }

    [Fact]
    public void JsonStj_SingleField_RoundTrip()
    {
        var c = JsonShapeBtStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeBtStj>(json);
        Assert.Equal(JsonShapeBtStj.Kind.Circle, d.tag);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonStj_MultiField_RoundTrip()
    {
        var r = JsonShapeBtStj.Rectangle(1.5f, 2.5f);
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeBtStj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonStj_Fieldless_RoundTrip()
    {
        var p = JsonShapeBtStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeBtStj>(json);
        Assert.Equal(JsonShapeBtStj.Kind.Point, d.tag);
    }

    [Fact]
    public void JsonNsj_SingleField_RoundTrip()
    {
        var c = JsonShapeBtNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonShapeBtNsj>(json);
        Assert.Equal(JsonShapeBtNsj.Kind.Circle, d.tag);
        Assert.Equal(3.14, d.radius);
    }

    [Fact]
    public void JsonNsj_MultiField_RoundTrip()
    {
        var r = JsonShapeBtNsj.Rectangle(1.5f, 2.5f);
        var json = JsonConvert.SerializeObject(r);
        var d = JsonConvert.DeserializeObject<JsonShapeBtNsj>(json);
        Assert.Equal(1.5f, d.width);
        Assert.Equal(2.5f, d.height);
    }

    [Fact]
    public void JsonNsj_Fieldless_RoundTrip()
    {
        var p = JsonShapeBtNsj.Point();
        var json = JsonConvert.SerializeObject(p);
        var d = JsonConvert.DeserializeObject<JsonShapeBtNsj>(json);
        Assert.Equal(JsonShapeBtNsj.Kind.Point, d.tag);
    }
}
