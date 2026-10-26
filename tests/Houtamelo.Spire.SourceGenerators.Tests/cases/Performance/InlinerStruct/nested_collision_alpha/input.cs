using Houtamelo.Spire;

namespace Collision.NestedAlpha;

public partial class Outer
{
    public partial class Inner
    {
        [InlinerStruct]
        public static int Run(int value) => value + 3;
    }
}
