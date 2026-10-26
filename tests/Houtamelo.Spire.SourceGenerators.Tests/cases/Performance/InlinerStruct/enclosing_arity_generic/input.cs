using Houtamelo.Spire;

namespace Collision.Arity;

public partial class Container
{
    public partial class Inner<T>
        where T : unmanaged
    {
        [InlinerStruct]
        public static T Run(T value) => value;
    }
}
