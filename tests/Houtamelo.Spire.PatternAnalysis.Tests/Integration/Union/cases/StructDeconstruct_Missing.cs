//@ not_exhaustive
// Missing Triangle
public class StructDeconstruct_Missing
{
    public int Test(Shape s) => s switch
    {
        (Shape.Kind.Circle, _) => 1,
        (Shape.Kind.Rectangle, _) => 2,
        //~ (Shape.Kind.Triangle, _)
    };
}
