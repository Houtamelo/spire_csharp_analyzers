using Houtamelo.Spire;

namespace TestNs
{
    public partial class Outer
    {
        [DiscriminatedUnion]
        internal partial struct Shape
        {
            [Variant] public static partial Shape Circle(double radius);
            [Variant] public static partial Shape Square(int sideLength);
        }
    }
}
