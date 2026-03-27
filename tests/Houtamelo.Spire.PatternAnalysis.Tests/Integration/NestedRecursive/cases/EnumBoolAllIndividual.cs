//@ exhaustive
// (Shape, bool) — all 4 combinations listed individually
public class EnumBoolAllIndividual
{
    public int Test(Shape shape, bool condition) => (shape, condition) switch
    {
        (Shape.Circle, true) => 1,
        (Shape.Circle, false) => 2,
        (Shape.Rectangle, true) => 3,
        (Shape.Rectangle, false) => 4,
    };
}
