using System;
using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class ValueSemanticsTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void DefaultStruct_TagIsZero(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");

        // default(Shape) has tag == 0 → first variant (Circle)
        var defaultInstance = Activator.CreateInstance(shapeType)!;
        var tag = ReadTag(defaultInstance);
        var firstKind = GetKindValue(shapeType, "Circle");
        Assert.Equal(firstKind, tag);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void StructIsValueType(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        Assert.True(shapeType.IsValueType);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void KindEnumExists(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var kindType = shapeType.GetNestedType("Kind");
        Assert.NotNull(kindType);
        Assert.True(kindType.IsEnum);

        var names = Enum.GetNames(kindType);
        Assert.Contains("Circle", names);
        Assert.Contains("Square", names);
        Assert.Contains("Rectangle", names);
        Assert.Contains("Point", names);
    }
}
