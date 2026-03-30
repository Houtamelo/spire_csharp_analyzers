using Houtamelo.Spire;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
public abstract partial record JsonShapeRecStj
{
    public sealed partial record Circle(double Radius) : JsonShapeRecStj;
    public sealed partial record Square(int Side) : JsonShapeRecStj;
    public sealed partial record Point() : JsonShapeRecStj;
}

[DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
public abstract partial record JsonShapeRecNsj
{
    public sealed partial record Circle(double Radius) : JsonShapeRecNsj;
    public sealed partial record Square(int Side) : JsonShapeRecNsj;
    public sealed partial record Point() : JsonShapeRecNsj;
}
