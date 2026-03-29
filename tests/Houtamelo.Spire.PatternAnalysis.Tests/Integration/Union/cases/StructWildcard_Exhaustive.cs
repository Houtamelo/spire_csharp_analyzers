//@ exhaustive
// Wildcard covers all remaining variants
public class StructWildcard_Exhaustive
{
    public int Test(Shape s) => s switch
    {
        (Shape.Kind.Circle, _) => 1,
        _ => 2,
    };
}
