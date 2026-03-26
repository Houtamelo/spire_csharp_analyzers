using System;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public class LargeUnionTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void EightVariants_AllKindsCorrect(string strategy)
    {
        var asm = CompileAndLoad(LargeUnionSource(strategy));
        var eventType = GetType(asm, "Event");

        string[] variants =
        {
            "Point", "Circle", "Label", "Rectangle",
            "ColoredLine", "Transform", "RichText", "Error"
        };

        foreach (var name in variants)
        {
            var expectedKind = GetKindValue(eventType, name);
            object instance = name switch
            {
                "Point" => InvokeFactory(eventType, name),
                "Circle" => InvokeFactory(eventType, name, 3.14),
                "Label" => InvokeFactory(eventType, name, "hello"),
                "Rectangle" => InvokeFactory(eventType, name, 1.5f, 2.5f),
                "ColoredLine" => InvokeFactory(eventType, name, 1, 2, "red"),
                "Transform" => InvokeFactory(eventType, name, 1.0f, 2.0f, 3.0f, 4.0f),
                "RichText" => InvokeFactory(eventType, name, "text", 12, true, "Arial", 1.5),
                "Error" => InvokeFactory(eventType, name, "oops"),
                _ => throw new Exception()
            };
            Assert.Equal(expectedKind, ReadKind(instance));
        }
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void EightVariants_FieldRoundTrip(string strategy)
    {
        var asm = CompileAndLoad(LargeUnionSource(strategy));
        var eventType = GetType(asm, "Event");

        // Circle
        var circle = InvokeFactory(eventType, "Circle", 3.14);
        Assert.Equal(3.14, ReadProperty(circle, "radius"));

        // Rectangle
        var rect = InvokeFactory(eventType, "Rectangle", 1.5f, 2.5f);
        Assert.Equal(1.5f, ReadProperty(rect, "width"));
        Assert.Equal(2.5f, ReadProperty(rect, "height"));

        // ColoredLine
        var line = InvokeFactory(eventType, "ColoredLine", 10, 20, "red");
        Assert.Equal(10, ReadProperty(line, "x1"));
        Assert.Equal(20, ReadProperty(line, "y1"));
        Assert.Equal("red", ReadProperty(line, "color"));

        // Transform
        var xform = InvokeFactory(eventType, "Transform", 1.0f, 2.0f, 3.0f, 4.0f);
        Assert.Equal(1.0f, ReadProperty(xform, "x"));
        Assert.Equal(2.0f, ReadProperty(xform, "y"));
        Assert.Equal(3.0f, ReadProperty(xform, "z"));
        Assert.Equal(4.0f, ReadProperty(xform, "w"));

        // RichText (5 fields, mixed)
        var rt = InvokeFactory(eventType, "RichText", "text", 12, true, "Arial", 1.5);
        Assert.Equal("text", ReadProperty(rt, "text"));
        Assert.Equal(12, ReadProperty(rt, "size"));
        Assert.Equal(true, ReadProperty(rt, "bold"));
        Assert.Equal("Arial", ReadProperty(rt, "font"));
        Assert.Equal(1.5, ReadProperty(rt, "spacing"));

        // Label
        var label = InvokeFactory(eventType, "Label", "hello");
        Assert.Equal("hello", ReadProperty(label, "text"));

        // Error
        var err = InvokeFactory(eventType, "Error", "oops");
        Assert.Equal("oops", ReadProperty(err, "message"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void EightVariants_KindIsByte(string strategy)
    {
        var asm = CompileAndLoad(LargeUnionSource(strategy));
        var eventType = GetType(asm, "Event");
        var kindType = eventType.GetNestedType("Kind")!;
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(kindType));
    }
}
