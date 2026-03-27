//@ not_exhaustive
// (Shape, bool) — Circle only covered for true, missing Circle+false
public class EnumBoolMissing
{
    public int Test(Shape shape, bool condition) => (shape, condition) switch
    {
        (Shape.Circle, true) => 1,
        (Shape.Rectangle, _) => 3,
    };
}
