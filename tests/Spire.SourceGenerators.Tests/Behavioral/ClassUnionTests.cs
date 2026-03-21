using System;
using System.Reflection;
using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class ClassUnionTests : BehavioralTestBase
{
    private static Assembly Asm => CompileAndLoad(ClassUnionSource);

    [Fact]
    public void Construction_Works()
    {
        var stopType = GetType(Asm, "TestNs.Command+Stop");
        var instance = Activator.CreateInstance(stopType, "shutdown")!;
        Assert.NotNull(instance);
    }

    [Fact]
    public void FieldAccess_Works()
    {
        var stopType = GetType(Asm, "TestNs.Command+Stop");
        var stop = Activator.CreateInstance(stopType, "shutdown")!;
        Assert.Equal("shutdown", ReadProperty(stop, "Reason"));
    }

    [Fact]
    public void TypePattern_Matching()
    {
        var runner = GetType(Asm, "TestNs.TestRunner");

        var startType = GetType(Asm, "TestNs.Command+Start");
        var start = Activator.CreateInstance(startType)!;
        Assert.Equal("start", InvokeMethod(runner, "TypeMatch", start));

        var stopType = GetType(Asm, "TestNs.Command+Stop");
        var stop = Activator.CreateInstance(stopType, "reason1")!;
        Assert.Equal("stop:reason1", InvokeMethod(runner, "TypeMatch", stop));
    }

    [Fact]
    public void Polymorphism_BaseRef()
    {
        var cmdType = GetType(Asm, "TestNs.Command");
        var startType = GetType(Asm, "TestNs.Command+Start");
        Assert.True(cmdType.IsAssignableFrom(startType));
    }

    [Fact]
    public void Sealed_Verified()
    {
        Assert.True(GetType(Asm, "TestNs.Command+Start").IsSealed);
        Assert.True(GetType(Asm, "TestNs.Command+Stop").IsSealed);
    }
}
