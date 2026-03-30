using Houtamelo.Spire;

namespace TestNs;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
public partial record Shape
{
    public partial record Circle(double Radius) : Shape;
    public partial record Rectangle(float Width, float Height) : Shape;
    public partial record Point() : Shape;
}
