using System;
using System.Reflection;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public class DeconstructTests : BehavioralTestBase
{
    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void VariantPayload_DeconstructHasTypedFields(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var rectangle = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);
        var payload = ReadPayload(rectangle, "rectangle");
        var method = payload.GetType().GetMethod("Deconstruct",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.Equal(2, method!.GetParameters().Length);

        var values = new object?[] { null, null };
        method.Invoke(payload, values);
        Assert.Equal(1.5f, values[0]);
        Assert.Equal(2.5f, values[1]);
        Assert.All(method.GetParameters(), parameter =>
            Assert.Equal(typeof(float).MakeByRefType(), parameter.ParameterType));
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void FieldlessPayload_HasEmptyDeconstruct(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var point = InvokeFactory(shapeType, "Point");
        var payload = ReadPayload(point, "point");
        var method = payload.GetType().GetMethod("Deconstruct",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.Empty(method!.GetParameters());
        method.Invoke(payload, Array.Empty<object>());
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void PayloadDeconstruct_DoesNotIncludeKind(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);
        var payload = ReadPayload(circle, "circle");
        var method = payload.GetType().GetMethod("Deconstruct",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.Single(method!.GetParameters());
        Assert.Equal(typeof(double).MakeByRefType(), method.GetParameters()[0].ParameterType);
    }
}
