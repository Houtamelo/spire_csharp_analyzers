using Houtamelo.Spire;

namespace Collision.NestedBeta;

public partial class Outer
{
    public partial class Inner
    {
        [InlinerStruct]
        public static int Run(int value) => value + 4;
    }
}
