using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class ReferenceTypeFieldTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void StringField_Preserved(string strategy)
    {
        var asm = CompileAndLoad(RefFieldsSource(strategy));
        var msgType = GetType(asm, "Message");
        var label = InvokeFactory(msgType, "Label", "hello");
        Assert.Equal("hello", ReadProperty(label, "text"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void NullString_Preserved(string strategy)
    {
        var asm = CompileAndLoad(RefFieldsSource(strategy));
        var msgType = GetType(asm, "Message");
        var label = InvokeFactory(msgType, "Label", (object)null!);
        Assert.Null(ReadProperty(label, "text"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void MixedRefAndValue_Preserved(string strategy)
    {
        var asm = CompileAndLoad(RefFieldsSource(strategy));
        var msgType = GetType(asm, "Message");
        var line = InvokeFactory(msgType, "ColoredLine", 10, 20, "red");
        Assert.Equal(10, ReadProperty(line, "x"));
        Assert.Equal(20, ReadProperty(line, "y"));
        Assert.Equal("red", ReadProperty(line, "color"));
    }
}
