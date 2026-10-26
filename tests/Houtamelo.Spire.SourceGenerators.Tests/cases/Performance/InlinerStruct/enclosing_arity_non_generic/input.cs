using Houtamelo.Spire;

namespace Collision.Arity;

public partial class Container
{
    public partial class Inner
    {
        [InlinerStruct]
        public static int Run(int value) => value;
    }
}
