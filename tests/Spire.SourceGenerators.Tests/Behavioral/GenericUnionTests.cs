using System;
using System.Reflection;
using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class GenericUnionTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(GenericCapableStrategies))]
    public void Option_Int_SomeRoundTrip(string strategy)
    {
        var asm = CompileAndLoad(GenericOptionSource(strategy));
        var openType = asm.GetType("Option`1")!;
        var optionInt = openType.MakeGenericType(typeof(int));

        var some = InvokeFactory(optionInt, "Some", 42);
        Assert.Equal(GetKindValue(optionInt, "Some"), ReadKind(some));
        Assert.Equal(42, ReadProperty(some, "value"));
    }

    [Theory]
    [MemberData(nameof(GenericCapableStrategies))]
    public void Option_String_SomeRoundTrip(string strategy)
    {
        var asm = CompileAndLoad(GenericOptionSource(strategy));
        var openType = asm.GetType("Option`1")!;
        var optionStr = openType.MakeGenericType(typeof(string));

        var some = InvokeFactory(optionStr, "Some", "hello");
        Assert.Equal(GetKindValue(optionStr, "Some"), ReadKind(some));
        Assert.Equal("hello", ReadProperty(some, "value"));
    }

    [Theory]
    [MemberData(nameof(GenericCapableStrategies))]
    public void Option_None_KindCorrect(string strategy)
    {
        var asm = CompileAndLoad(GenericOptionSource(strategy));
        var openType = asm.GetType("Option`1")!;
        var optionInt = openType.MakeGenericType(typeof(int));

        var none = InvokeFactory(optionInt, "None");
        Assert.Equal(GetKindValue(optionInt, "None"), ReadKind(none));
    }
}
