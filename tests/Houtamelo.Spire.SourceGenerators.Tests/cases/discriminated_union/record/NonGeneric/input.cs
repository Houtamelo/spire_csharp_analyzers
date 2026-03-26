using Spire;

namespace Shapes
{
    [DiscriminatedUnion]
    public partial record Shape
    {
        public partial record Circle(double Radius) : Shape;
        public partial record Square(int Side) : Shape;
    }
}
