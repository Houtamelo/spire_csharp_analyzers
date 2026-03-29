//@ not_exhaustive
// Missing Triangle
public class StructProperty_Missing
{
    public int Test(Shape s) => s switch
    {
        { kind: Shape.Kind.Circle } => 1,
        { kind: Shape.Kind.Rectangle } => 2,
        //~ { kind: Shape.Kind.Triangle }
    };
}
