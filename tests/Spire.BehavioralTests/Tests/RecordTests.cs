using Xunit;

namespace Spire.BehavioralTests.Tests;

public class RecordTests
{
    [Fact]
    public void Construction_And_FieldAccess()
    {
        var c = new ShapeRec.Circle(3.14);
        Assert.Equal(3.14, c.Radius);

        var s = new ShapeRec.Square(5);
        Assert.Equal(5, s.Side);
    }

    [Fact]
    public void TypePatternMatching()
    {
        ShapeRec shape = new ShapeRec.Circle(3.14);
        var result = shape switch
        {
            ShapeRec.Circle c => $"circle:{c.Radius}",
            ShapeRec.Square s => $"square:{s.Side}",
            ShapeRec.Point => "point",
            _ => "unknown"
        };
        Assert.Equal("circle:3.14", result);
    }

    [Fact]
    public void AllVariants_Match()
    {
        Assert.Equal("circle", Match(new ShapeRec.Circle(1)));
        Assert.Equal("square", Match(new ShapeRec.Square(1)));
        Assert.Equal("point", Match(new ShapeRec.Point()));

        static string Match(ShapeRec s) => s switch
        {
            ShapeRec.Circle => "circle",
            ShapeRec.Square => "square",
            ShapeRec.Point => "point",
            _ => "unknown"
        };
    }

    [Fact]
    public void Polymorphism()
    {
        ShapeRec shape = new ShapeRec.Circle(3.14);
        Assert.IsType<ShapeRec.Circle>(shape);
        Assert.IsAssignableFrom<ShapeRec>(shape);
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new ShapeRec.Circle(3.14);
        var b = new ShapeRec.Circle(3.14);
        Assert.Equal(a, b);
        Assert.NotEqual<ShapeRec>(a, new ShapeRec.Square(1));
    }

    [Fact]
    public void Sealed_And_Abstract()
    {
        Assert.True(typeof(ShapeRec).IsAbstract);
        Assert.True(typeof(ShapeRec.Circle).IsSealed);
        Assert.True(typeof(ShapeRec.Square).IsSealed);
        Assert.True(typeof(ShapeRec.Point).IsSealed);
    }
}
