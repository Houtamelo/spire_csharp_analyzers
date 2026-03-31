using System.Runtime.InteropServices;
using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

/// Validates that DU MSBuild defaults are applied through the full pipeline.
public class DUDefaultsTests
{
    [Fact]
    public void Shape_UsesAdditiveLayout_FromGlobalConfig()
    {
        // Global config: Spire_DU_DefaultLayout=Additive
        // Additive does NOT use LayoutKind.Explicit (that's Overlap).
        var layout = typeof(Shape).StructLayoutAttribute;
        Assert.NotNull(layout);
        Assert.NotEqual(LayoutKind.Explicit, layout!.Value);
    }

    [Fact]
    public void Event_UsesOverlapLayout_ExplicitOverridesGlobal()
    {
        // [DiscriminatedUnion(layout: Layout.Overlap)] overrides global Additive
        // Overlap uses LayoutKind.Explicit with [FieldOffset]
        var layout = typeof(Event).StructLayoutAttribute;
        Assert.NotNull(layout);
        Assert.Equal(LayoutKind.Explicit, layout!.Value);
    }

    [Fact]
    public void Shape_HasDeconstructMethod()
    {
        // Global config: Spire_DU_DefaultGenerateDeconstruct=true
        var shape = Shape.Circle(5f);
        Assert.True(shape.IsCircle);
        shape.Deconstruct(out Shape.Kind _, out float radius);
        Assert.Equal(5f, radius);
    }

    [Fact]
    public void Event_HasDeconstructMethod()
    {
        var ev = Event.Click(10, 20);
        Assert.True(ev.IsClick);
        ev.Deconstruct(out Event.Kind _, out int x, out int y);
        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void Shape_HasKindEnum()
    {
        // All DU structs generate a Kind enum
        var shape = Shape.Circle(3f);
        Assert.Equal(Shape.Kind.Circle, shape.kind);
    }

    [Fact]
    public void Shape_IsVariantProperties()
    {
        var circle = Shape.Circle(1f);
        Assert.True(circle.IsCircle);
        Assert.False(circle.IsRect);

        var rect = Shape.Rect(2f, 3f);
        Assert.False(rect.IsCircle);
        Assert.True(rect.IsRect);
    }

    [Fact]
    public void Event_OverlapLayout_FieldAccess()
    {
        // Overlap layout overlaps fields — verify correct storage/retrieval
        var click = Event.Click(42, 99);
        click.Deconstruct(out Event.Kind kind, out int x, out int y);
        Assert.Equal(Event.Kind.Click, kind);
        Assert.Equal(42, x);
        Assert.Equal(99, y);

        var key = Event.Key('A');
        key.Deconstruct(out Event.Kind keyKind, out char ch);
        Assert.Equal(Event.Kind.Key, keyKind);
        Assert.Equal('A', ch);
    }
}
