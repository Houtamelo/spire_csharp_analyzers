using System.Text.Json;
using Newtonsoft.Json;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class AdditiveTests
{
    // ── Factory + Property Round-Trip ────────────────────────────

    [Fact]
    public void Circle_TagAndRadius()
    {
        var c = ShapeAdd.Circle(3.14);
        Assert.Equal(ShapeAdd.Kind.Circle, c.kind);
        Assert.Equal(3.14, c.circle.radius);
    }

    [Fact]
    public void Square_TagAndSideLength()
    {
        var s = ShapeAdd.Square(42);
        Assert.Equal(ShapeAdd.Kind.Square, s.kind);
        Assert.Equal(42, s.square.sideLength);
    }

    [Fact]
    public void Rectangle_AllFields()
    {
        var r = ShapeAdd.Rectangle(1.5f, 2.5f);
        Assert.Equal(ShapeAdd.Kind.Rectangle, r.kind);
        Assert.Equal(1.5f, r.rectangle.width);
        Assert.Equal(2.5f, r.rectangle.height);
    }

    [Fact]
    public void Point_Tag()
    {
        var p = ShapeAdd.Point();
        Assert.Equal(ShapeAdd.Kind.Point, p.kind);
    }

    // ── Switch on Tag ───────────────────────────────────────────

    [Fact]
    public void SwitchOnTag_AllBranches()
    {
        Assert.Equal("circle", Match(ShapeAdd.Circle(1)));
        Assert.Equal("square", Match(ShapeAdd.Square(1)));
        Assert.Equal("rectangle", Match(ShapeAdd.Rectangle(1, 2)));
        Assert.Equal("point", Match(ShapeAdd.Point()));

        static string Match(ShapeAdd s) => s.kind switch
        {
            ShapeAdd.Kind.Circle => "circle",
            ShapeAdd.Kind.Square => "square",
            ShapeAdd.Kind.Rectangle => "rectangle",
            ShapeAdd.Kind.Point => "point",
            _ => "unknown"
        };
    }

    // ── Property Pattern Matching ───────────────────────────────

    [Fact]
    public void PropertyPattern_SingleField()
    {
        var c = ShapeAdd.Circle(7.5);
        var r = c switch
        {
            { kind: ShapeAdd.Kind.Circle, circle: { radius: var v } } => v,
            _ => -1.0
        };
        Assert.Equal(7.5, r);
    }

    [Fact]
    public void PropertyPattern_MultiField()
    {
        var rect = ShapeAdd.Rectangle(3.0f, 4.0f);
        var (w, h) = rect switch
        {
            { kind: ShapeAdd.Kind.Rectangle, rectangle: { width: var ww, height: var hh } } => (ww, hh),
            _ => (0f, 0f)
        };
        Assert.Equal(3.0f, w);
        Assert.Equal(4.0f, h);
    }

    [Fact]
    public void PropertyPattern_NonMatchReturnsDefault()
    {
        var s = ShapeAdd.Square(10);
        var r = s switch
        {
            { kind: ShapeAdd.Kind.Circle, circle: { radius: var v } } => v,
            _ => -1.0
        };
        Assert.Equal(-1.0, r);
    }

    // ── Positional Deconstruct ──────────────────────────────────

    [Fact]
    public void Deconstruct_UniqueArity()
    {
        var rect = ShapeAdd.Rectangle(1.5f, 2.5f);
        var payload = rect.rectangle;
        payload.Deconstruct(out var w, out var h);
        Assert.Equal(1.5f, w);
        Assert.Equal(2.5f, h);
    }

    [Fact]
    public void Deconstruct_SharedArity()
    {
        var c = ShapeAdd.Circle(3.14);
        c.circle.Deconstruct(out var f0);
        Assert.Equal(3.14, f0);
    }

    // ── Shared Fields ───────────────────────────────────────────

    [Fact]
    public void SharedFields_Vec2()
    {
        var v = VecAdd.Vec2(1.0, 2.0);
        Assert.Equal(1.0, v.vec2.x);
        Assert.Equal(2.0, v.vec2.y);
    }

    [Fact]
    public void SharedFields_Vec3()
    {
        var v = VecAdd.Vec3(3.0, 4.0, 5.0);
        Assert.Equal(3.0, v.vec3.x);
        Assert.Equal(4.0, v.vec3.y);
        Assert.Equal(5.0, v.vec3.z);
    }

    // ── Reference Type Fields ───────────────────────────────────

    [Fact]
    public void StringField_Preserved()
    {
        var m = MsgAdd.Label("hello");
        Assert.Equal("hello", m.label.text);
    }

    [Fact]
    public void MixedRefAndValue()
    {
        var m = MsgAdd.ColoredLine(10, 20, "red");
        Assert.Equal(10, m.coloredLine.x);
        Assert.Equal(20, m.coloredLine.y);
        Assert.Equal("red", m.coloredLine.color);
    }

    // ── Generic ─────────────────────────────────────────────────

    [Fact]
    public void Option_Int_Some()
    {
        var o = OptionAdd<int>.Some(42);
        Assert.Equal(OptionAdd<int>.Kind.Some, o.kind);
        Assert.Equal(42, o.some.value);
    }

    [Fact]
    public void Option_String_Some()
    {
        var o = OptionAdd<string>.Some("hi");
        Assert.Equal(OptionAdd<string>.Kind.Some, o.kind);
        Assert.Equal("hi", o.some.value);
    }

    [Fact]
    public void Option_None()
    {
        var o = OptionAdd<int>.None();
        Assert.Equal(OptionAdd<int>.Kind.None, o.kind);
    }

    [Fact]
    public void Copy_PreservesVariantPayload()
    {
        var original = ShapeAdd.Rectangle(1.5f, 2.5f);
        var copy = original;

        Assert.Equal(ShapeAdd.Kind.Rectangle, copy.kind);
        Assert.Equal(1.5f, copy.rectangle.width);
        Assert.Equal(2.5f, copy.rectangle.height);
    }

    [Fact]
    public void GenericReferenceNull_IsPreserved()
    {
        var value = OptionAdd<string>.Some(null!);

        Assert.Equal(OptionAdd<string>.Kind.Some, value.kind);
        Assert.Null(value.some.value);
    }

    // ── JSON System.Text.Json ───────────────────────────────────

    [Fact]
    public void JsonStj_CircleRoundTrip()
    {
        var c = JsonShapeAddStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json);
        Assert.Equal(JsonShapeAddStj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.circle.radius);
    }

    [Fact]
    public void JsonStj_MultiField_RoundTrip()
    {
        var r = JsonShapeAddStj.Rectangle(1.5f, 2.5f);
        var json = System.Text.Json.JsonSerializer.Serialize(r);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json);
        Assert.Equal(JsonShapeAddStj.Kind.Rectangle, d.kind);
        Assert.Equal(1.5f, d.rectangle.width);
        Assert.Equal(2.5f, d.rectangle.height);
    }

    [Fact]
    public void JsonStj_Fieldless_RoundTrip()
    {
        var p = JsonShapeAddStj.Point();
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var d = System.Text.Json.JsonSerializer.Deserialize<JsonShapeAddStj>(json);
        Assert.Equal(JsonShapeAddStj.Kind.Point, d.kind);
    }

    [Fact]
    public void JsonStj_DiscriminatorPresent()
    {
        var c = JsonShapeAddStj.Circle(3.14);
        var json = System.Text.Json.JsonSerializer.Serialize(c);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }

    [Fact]
    public void JsonStj_AllVariants_Discriminators()
    {
        var j1 = System.Text.Json.JsonSerializer.Serialize(JsonShapeAddStj.Circle(1));
        Assert.Contains("\"Circle\"", j1);
        var j2 = System.Text.Json.JsonSerializer.Serialize(JsonShapeAddStj.Rectangle(1, 2));
        Assert.Contains("\"Rectangle\"", j2);
        var j3 = System.Text.Json.JsonSerializer.Serialize(JsonShapeAddStj.Point());
        Assert.Contains("\"Point\"", j3);
    }

    // ── JSON Newtonsoft ─────────────────────────────────────────

    [Fact]
    public void JsonNsj_CircleRoundTrip()
    {
        var c = JsonShapeAddNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        var d = JsonConvert.DeserializeObject<JsonShapeAddNsj>(json);
        Assert.Equal(JsonShapeAddNsj.Kind.Circle, d.kind);
        Assert.Equal(3.14, d.circle.radius);
    }

    [Fact]
    public void JsonNsj_MultiField_RoundTrip()
    {
        var r = JsonShapeAddNsj.Rectangle(1.5f, 2.5f);
        var json = JsonConvert.SerializeObject(r);
        var d = JsonConvert.DeserializeObject<JsonShapeAddNsj>(json);
        Assert.Equal(JsonShapeAddNsj.Kind.Rectangle, d.kind);
        Assert.Equal(1.5f, d.rectangle.width);
        Assert.Equal(2.5f, d.rectangle.height);
    }

    [Fact]
    public void JsonNsj_Fieldless_RoundTrip()
    {
        var p = JsonShapeAddNsj.Point();
        var json = JsonConvert.SerializeObject(p);
        var d = JsonConvert.DeserializeObject<JsonShapeAddNsj>(json);
        Assert.Equal(JsonShapeAddNsj.Kind.Point, d.kind);
    }

    [Fact]
    public void JsonNsj_DiscriminatorPresent()
    {
        var c = JsonShapeAddNsj.Circle(3.14);
        var json = JsonConvert.SerializeObject(c);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"Circle\"", json);
    }
}
