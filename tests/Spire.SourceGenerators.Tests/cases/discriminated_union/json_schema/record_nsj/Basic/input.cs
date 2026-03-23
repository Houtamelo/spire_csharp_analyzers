using Spire;

namespace Shapes
{
    [DiscriminatedUnion(Json = JsonLibrary.NewtonsoftJson)]
    public partial record Shape
    {
        public partial record Circle(double Radius) : Shape;
        public partial record Square(int Side) : Shape;
    }
}
