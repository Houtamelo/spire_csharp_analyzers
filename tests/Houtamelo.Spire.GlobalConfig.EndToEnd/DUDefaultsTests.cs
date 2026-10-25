using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

/// Validates that DU reference-storage defaults are applied through the full MSBuild pipeline.
public class DUDefaultsTests
{
    [Fact]
    public void Shape_UsesGlobalAutoReferenceStorage()
    {
        var circle = Shape.Circle(5f);
        Assert.True(circle.IsCircle);
        var radius = circle switch
        {
            { kind: Shape.Kind.Circle, circle: { radius: var value } } => value,
            _ => -1f,
        };
        Assert.Equal(5f, radius);
    }

    [Fact]
    public void Event_ExplicitTypedStorageOverridesGlobalDefault()
    {
        var click = Event.Click(10, 20);
        Assert.True(click.IsClick);
        var coordinates = click switch
        {
            { kind: Event.Kind.Click, click: (var x, var y) } => (x, y),
            _ => (-1, -1),
        };
        Assert.Equal(10, coordinates.Item1);
        Assert.Equal(20, coordinates.Item2);
    }

    [Fact]
    public void PayloadsHaveTypedDeconstructMethods()
    {
        var shapePayload = Shape.Rect(2f, 3f).rect;
        shapePayload.Deconstruct(out var width, out var height);
        Assert.Equal(2f, width);
        Assert.Equal(3f, height);

        var eventPayload = Event.Click(10, 20).click;
        eventPayload.Deconstruct(out var x, out var y);
        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void Shape_HasKindAndVariantProperties()
    {
        var circle = Shape.Circle(1f);
        Assert.Equal(Shape.Kind.Circle, circle.kind);
        Assert.True(circle.IsCircle);
        Assert.False(circle.IsRect);

        var rect = Shape.Rect(2f, 3f);
        Assert.False(rect.IsCircle);
        Assert.True(rect.IsRect);
    }
}
