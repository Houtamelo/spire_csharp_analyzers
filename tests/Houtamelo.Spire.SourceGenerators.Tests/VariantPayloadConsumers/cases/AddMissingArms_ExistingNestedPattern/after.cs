using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rectangle(float width, float height);
}

public static class Consumer
{
    public static float Match(Shape shape) => shape switch
    {
        { kind: Shape.Kind.Circle, circle: { radius: var radius } } => radius,
        { IsRectangle: true, rectangle: var (width, height) } => throw new System.NotImplementedException()
    };
}
