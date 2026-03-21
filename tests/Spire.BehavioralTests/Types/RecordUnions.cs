using Spire;

[DiscriminatedUnion]
public abstract partial record ShapeRec
{
    public sealed partial record Circle(double Radius) : ShapeRec;
    public sealed partial record Square(int Side) : ShapeRec;
    public sealed partial record Point() : ShapeRec;
}
