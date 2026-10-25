using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public sealed class OverlapConstructorTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Factories_PreservePrimitiveValuesAndCopies(string strategy)
    {
        var assembly = CompileAndLoad(BasicShapeSource(strategy));
        var unionType = GetType(assembly, "Shape");

        var circle = InvokeFactory(unionType, "Circle", 3.25d);
        Assert.Equal(GetKindValue(unionType, "Circle"), ReadKind(circle));
        Assert.Equal(3.25d, ReadProperty(circle, "radius"));

        var rectangle = InvokeFactory(unionType, "Rectangle", 4.5f, 8.25f);
        Assert.Equal(GetKindValue(unionType, "Rectangle"), ReadKind(rectangle));
        Assert.Equal(4.5f, ReadProperty(rectangle, "width"));
        Assert.Equal(8.25f, ReadProperty(rectangle, "height"));

        var rectangleCopy = RuntimeHelpers.GetObjectValue(rectangle);
        Assert.Equal(4.5f, ReadProperty(rectangleCopy, "width"));
        Assert.Equal(8.25f, ReadProperty(rectangleCopy, "height"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Factories_PreserveReferencesAndNulls(string strategy)
    {
        var assembly = CompileAndLoad(RefFieldsSource(strategy));
        var unionType = GetType(assembly, "Message");
        var text = new string(new[] { 't', 'e', 'x', 't' });
        var label = InvokeFactory(unionType, "Label", text);
        Assert.Same(text, ReadProperty(label, "text"));
        Assert.Null(ReadProperty(InvokeFactory(unionType, "Label", (object)null!), "text"));

        var line = InvokeFactory(unionType, "ColoredLine", 10, 20, "red");
        Assert.Equal(10, ReadProperty(line, "x"));
        Assert.Equal(20, ReadProperty(line, "y"));
        Assert.Equal("red", ReadProperty(line, "color"));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void PayloadTypes_ArePublicDataWithTypedDeconstruct(string strategy)
    {
        var assembly = CompileAndLoad(BasicShapeSource(strategy));
        var unionType = GetType(assembly, "Shape");
        var rectanglePayloadType = GetType(assembly, "ShapeTypes+Variants+Rectangle");
        Assert.True(rectanglePayloadType.IsValueType);
        Assert.NotNull(rectanglePayloadType.GetProperty("width", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(rectanglePayloadType.GetProperty("height", BindingFlags.Public | BindingFlags.Instance));
        var deconstruct = rectanglePayloadType.GetMethod("Deconstruct",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(deconstruct);
        Assert.Equal(2, deconstruct!.GetParameters().Length);

        var rectangle = InvokeFactory(unionType, "Rectangle", 1.0f, 2.0f);
        var payload = ReadPayload(rectangle, "rectangle");
        var values = new object?[] { null, null };
        deconstruct.Invoke(payload, values);
        Assert.Equal(1.0f, values[0]);
        Assert.Equal(2.0f, values[1]);
    }

    [Fact]
    public void IncompatibleFields_UseVariantPayloadsWithoutFlatProperties()
    {
        const string source = """
            using Houtamelo.Spire;
            [DiscriminatedUnion]
            public partial struct Result
            {
                [Variant] public static partial Result Count(int value);
                [Variant] public static partial Result Text(string value);
            }
            """;
        var assembly = CompileAndLoad(source);
        var unionType = GetType(assembly, "Result");
        Assert.Null(unionType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance));
        var count = InvokeFactory(unionType, "Count", 42);
        var text = InvokeFactory(unionType, "Text", "hello");
        Assert.Equal(42, ReadProperty(count, "value"));
        Assert.Equal("hello", ReadProperty(text, "value"));
        Assert.Equal("Count", ReadPayload(count, "count").GetType().Name);
        Assert.Equal("Text", ReadPayload(text, "text").GetType().Name);
    }
}
