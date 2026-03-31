using System.Runtime.InteropServices;
using Houtamelo.Spire.GlobalConfig.EndToEnd.Types;
using Xunit;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd;

public class DUDefaultsTests
{
    [Fact]
    public void Union_UsesAdditiveLayout()
    {
        // Additive layout uses LayoutKind.Auto (default struct layout).
        // Overlap uses LayoutKind.Explicit with [FieldOffset] attributes.
        // If global config Spire_DU_DefaultLayout=Additive is working,
        // Shape should NOT have LayoutKind.Explicit.
        var layout = typeof(Shape).StructLayoutAttribute;
        Assert.NotNull(layout);
        Assert.NotEqual(LayoutKind.Explicit, layout!.Value);
    }

    [Fact]
    public void Union_HasDeconstructMethod()
    {
        // Global config Spire_DU_DefaultGenerateDeconstruct=true
        var shape = Shape.Circle(5f);
        if (shape.IsCircle)
        {
            shape.Deconstruct(out Shape.Kind _, out float radius);
            Assert.Equal(5f, radius);
        }
    }
}
