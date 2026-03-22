using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class SharedFieldTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void SameNameSameType_BothVariantsCorrect(string strategy)
    {
        var asm = CompileAndLoad(SharedFieldsSource(strategy));
        var vecType = GetType(asm, "Vec");

        var v2 = InvokeFactory(vecType, "Vec2", 1.0, 2.0);
        Assert.Equal(1.0, ReadProperty(v2, "x"));
        Assert.Equal(2.0, ReadProperty(v2, "y"));

        var v3 = InvokeFactory(vecType, "Vec3", 3.0, 4.0, 5.0);
        Assert.Equal(3.0, ReadProperty(v3, "x"));
        Assert.Equal(4.0, ReadProperty(v3, "y"));
        Assert.Equal(5.0, ReadProperty(v3, "z"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void SharedField_KindsDistinct(string strategy)
    {
        var asm = CompileAndLoad(SharedFieldsSource(strategy));
        var vecType = GetType(asm, "Vec");

        var v2 = InvokeFactory(vecType, "Vec2", 1.0, 2.0);
        var v3 = InvokeFactory(vecType, "Vec3", 3.0, 4.0, 5.0);

        Assert.Equal(GetKindValue(vecType, "Vec2"), ReadKind(v2));
        Assert.Equal(GetKindValue(vecType, "Vec3"), ReadKind(v3));
        Assert.NotEqual(ReadKind(v2), ReadKind(v3));
    }
}
