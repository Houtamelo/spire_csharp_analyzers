using System;
using System.Reflection;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public class RecordUnionTests : BehavioralTestBase
{
    private static Assembly Asm => CompileAndLoad(RecordUnionSource);

    [Fact]
    public void Construction_Works()
    {
        var circleType = GetType(Asm, "TestNs.Shape+Circle");
        var instance = Activator.CreateInstance(circleType, 3.14)!;
        Assert.NotNull(instance);
    }

    [Fact]
    public void FieldAccess_Works()
    {
        var circleType = GetType(Asm, "TestNs.Shape+Circle");
        var circle = Activator.CreateInstance(circleType, 3.14)!;
        var radius = ReadProperty(circle, "Radius");
        Assert.Equal(3.14, radius);
    }

    [Fact]
    public void TypePattern_Matching()
    {
        var shapeType = GetType(Asm, "TestNs.Shape");
        var circleType = GetType(Asm, "TestNs.Shape+Circle");
        var runner = GetType(Asm, "TestNs.TestRunner");

        var circle = Activator.CreateInstance(circleType, 3.14)!;
        var result = InvokeMethod(runner, "TypeMatch", circle);
        Assert.Equal("circle:3.14", result);
    }

    [Fact]
    public void Polymorphism_BaseRef()
    {
        var shapeType = GetType(Asm, "TestNs.Shape");
        var circleType = GetType(Asm, "TestNs.Shape+Circle");
        var circle = Activator.CreateInstance(circleType, 3.14)!;

        // Circle is assignable to Shape
        Assert.True(shapeType.IsAssignableFrom(circleType));
        Assert.IsAssignableFrom(shapeType, circle);
    }

    [Fact]
    public void AllVariants_TypeMatch()
    {
        var runner = GetType(Asm, "TestNs.TestRunner");

        var circleType = GetType(Asm, "TestNs.Shape+Circle");
        var circle = Activator.CreateInstance(circleType, 3.14)!;
        Assert.Equal("circle:3.14", InvokeMethod(runner, "TypeMatch", circle));

        var squareType = GetType(Asm, "TestNs.Shape+Square");
        var square = Activator.CreateInstance(squareType, 5)!;
        Assert.Equal("square:5", InvokeMethod(runner, "TypeMatch", square));

        var pointType = GetType(Asm, "TestNs.Shape+Point");
        var point = Activator.CreateInstance(pointType)!;
        Assert.Equal("point", InvokeMethod(runner, "TypeMatch", point));
    }

    [Fact]
    public void Sealed_Verified()
    {
        Assert.True(GetType(Asm, "TestNs.Shape+Circle").IsSealed);
        Assert.True(GetType(Asm, "TestNs.Shape+Square").IsSealed);
        Assert.True(GetType(Asm, "TestNs.Shape+Point").IsSealed);
    }

    [Fact]
    public void Abstract_Verified()
    {
        Assert.True(GetType(Asm, "TestNs.Shape").IsAbstract);
    }
}
