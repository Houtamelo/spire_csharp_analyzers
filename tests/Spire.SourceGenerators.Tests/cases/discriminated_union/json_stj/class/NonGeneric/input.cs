using Spire;

namespace TestNs;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
public partial class Shape
{
    public partial class Circle(double Radius) : Shape;
    public partial class Rect(float Width, float Height) : Shape;
    public partial class Point() : Shape;
}
