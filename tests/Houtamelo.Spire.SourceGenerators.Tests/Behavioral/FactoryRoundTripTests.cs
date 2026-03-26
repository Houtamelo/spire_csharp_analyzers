using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public class FactoryRoundTripTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Circle_KindIsCorrect(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);
        var kind = ReadKind(circle);
        var expected = GetKindValue(shapeType, "Circle");
        Assert.Equal(expected, kind);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Circle_RadiusPreserved(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);
        var radius = ReadProperty(circle, "radius");
        Assert.Equal(3.14, radius);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Square_IntFieldPreserved(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var square = InvokeFactory(shapeType, "Square", 42);
        var sideLength = ReadProperty(square, "sideLength");
        Assert.Equal(42, sideLength);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Point_KindIsCorrect(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var point = InvokeFactory(shapeType, "Point");
        var kind = ReadKind(point);
        var expected = GetKindValue(shapeType, "Point");
        Assert.Equal(expected, kind);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void MultiField_AllFieldsPreserved(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var rect = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);
        Assert.Equal(1.5f, ReadProperty(rect, "width"));
        Assert.Equal(2.5f, ReadProperty(rect, "height"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void FieldlessVariant_NoThrow(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var ex = Record.Exception(() => InvokeFactory(shapeType, "Point"));
        Assert.Null(ex);
    }
}
