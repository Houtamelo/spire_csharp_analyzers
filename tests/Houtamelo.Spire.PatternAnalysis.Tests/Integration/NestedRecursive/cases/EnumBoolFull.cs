//@ exhaustive
// (Shape, bool) — all combinations via individual + wildcard arms
public class EnumBoolFull
{
    public int Test(Shape shape, bool condition) => (shape, condition) switch
    {
        (Shape.Circle, true) => 1,
        (Shape.Circle, false) => 2,
        (Shape.Rectangle, _) => 3,
    };
}
