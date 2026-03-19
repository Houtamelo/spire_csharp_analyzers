using Spire;

namespace Geo
{
    [DiscriminatedUnion]
    public partial record Shape
    {
        public partial record Rectangle(float Width, float Height) : Shape;
        public partial record Circle(double Radius) : Shape;
    }
}
