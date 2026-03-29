//@ exhaustive
// All Kind variants covered via deconstruct
public class StructDeconstruct_Exhaustive
{
    public int Test(Shape s) => s switch
    {
        (Shape.Kind.Circle, _) => 1,
        (Shape.Kind.Rectangle, _) => 2,
        (Shape.Kind.Triangle, _) => 3,
    };
}
