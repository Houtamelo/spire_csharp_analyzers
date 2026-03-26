using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public class PatternMatchingTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void SwitchOnKind_AllBranches(string strategy)
    {
        var asm = CompileAndLoad(PatternMatchingSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var runner = GetType(asm, "TestRunner");

        var circle = InvokeFactory(shapeType, "Circle", 3.14);
        Assert.Equal("circle", InvokeMethod(runner, "MatchKind", circle));

        var square = InvokeFactory(shapeType, "Square", 42);
        Assert.Equal("square", InvokeMethod(runner, "MatchKind", square));

        var rect = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);
        Assert.Equal("rectangle", InvokeMethod(runner, "MatchKind", rect));

        var point = InvokeFactory(shapeType, "Point");
        Assert.Equal("point", InvokeMethod(runner, "MatchKind", point));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void PropertyPattern_ExtractsField(string strategy)
    {
        var asm = CompileAndLoad(PatternMatchingSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var runner = GetType(asm, "TestRunner");

        var circle = InvokeFactory(shapeType, "Circle", 7.5);
        Assert.Equal(7.5, InvokeMethod(runner, "ExtractRadius", circle));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void PropertyPattern_MultiField(string strategy)
    {
        var asm = CompileAndLoad(PatternMatchingSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var runner = GetType(asm, "TestRunner");

        var rect = InvokeFactory(shapeType, "Rectangle", 3.0f, 4.0f);
        Assert.Equal("3,4", InvokeMethod(runner, "ExtractMultiField", rect));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void PropertyPattern_NonMatchReturnsDefault(string strategy)
    {
        var asm = CompileAndLoad(PatternMatchingSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var runner = GetType(asm, "TestRunner");

        var square = InvokeFactory(shapeType, "Square", 10);
        Assert.Equal(-1.0, InvokeMethod(runner, "ExtractRadius", square));
        Assert.Equal("none", InvokeMethod(runner, "ExtractMultiField", square));
    }
}
