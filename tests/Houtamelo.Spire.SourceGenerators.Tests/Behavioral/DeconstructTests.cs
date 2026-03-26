using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Spire.SourceGenerators.Tests.Behavioral;

public class DeconstructTests : BehavioralTestBase
{
    /// BoxedTuple only emits Deconstruct(out Kind, out object?) — no typed overloads.
    public static IEnumerable<object[]> StrategiesWithTypedDeconstruct =>
        StructStrategies.Where(s => (string)s[0] != "Layout.BoxedTuple");

    /// Convert boxed Kind enum to int for comparison with GetKindValue result.
    private static int KindToInt(object? boxedKind) => Convert.ToInt32(boxedKind!);

    [Theory]
    [MemberData(nameof(StrategiesWithTypedDeconstruct))]
    public void UniqueArity_TypedParams(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var rect = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);

        // Rectangle has unique arity 2 → typed Deconstruct(out Kind, out float, out float)
        var results = InvokeDeconstruct(rect, 3);
        Assert.Equal(GetKindValue(shapeType, "Rectangle"), KindToInt(results[0]));
        Assert.Equal(1.5f, results[1]);
        Assert.Equal(2.5f, results[2]);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void SharedArity_ObjectParams(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var circle = InvokeFactory(shapeType, "Circle", 3.14);

        // Circle and Square both have arity 1 → shared Deconstruct(out Kind, out object?)
        var results = InvokeDeconstruct(circle, 2);
        Assert.Equal(GetKindValue(shapeType, "Circle"), KindToInt(results[0]));
        Assert.Equal(3.14, results[1]);
    }

    [Theory]
    [MemberData(nameof(StructStrategies))]
    public void Fieldless_DeconstructReturnsNull(string strategy)
    {
        var asm = CompileAndLoad(BasicShapeSource(strategy));
        var shapeType = GetType(asm, "Shape");
        var point = InvokeFactory(shapeType, "Point");

        var results = InvokeDeconstruct(point, 2);
        Assert.Equal(GetKindValue(shapeType, "Point"), KindToInt(results[0]));
        Assert.Null(results[1]);
    }

    [Fact]
    public void BoxedTuple_PayloadDeconstruct()
    {
        var asm = CompileAndLoad(BasicShapeSource("Layout.BoxedTuple"));
        var shapeType = GetType(asm, "Shape");

        // BoxedTuple only emits Deconstruct(out Kind, out object?)
        var rect = InvokeFactory(shapeType, "Rectangle", 1.5f, 2.5f);
        var results = InvokeDeconstruct(rect, 2);
        Assert.Equal(GetKindValue(shapeType, "Rectangle"), KindToInt(results[0]));
        Assert.NotNull(results[1]); // payload is a boxed tuple

        var circle = InvokeFactory(shapeType, "Circle", 3.14);
        results = InvokeDeconstruct(circle, 2);
        Assert.Equal(GetKindValue(shapeType, "Circle"), KindToInt(results[0]));
        Assert.Equal(3.14, results[1]); // single-field payload is the value directly
    }
}
