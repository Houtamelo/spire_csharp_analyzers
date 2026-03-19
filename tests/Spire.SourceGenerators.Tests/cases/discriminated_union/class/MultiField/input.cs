using Spire;

namespace MyApp
{
    [DiscriminatedUnion]
    public partial class Shape
    {
        public partial class Circle : Shape
        {
            public double Radius { get; }
            public Circle(double radius) { Radius = radius; }
        }
        public partial class Rect : Shape
        {
            public double Width { get; }
            public double Height { get; }
            public Rect(double width, double height) { Width = width; Height = height; }
        }
    }
}
