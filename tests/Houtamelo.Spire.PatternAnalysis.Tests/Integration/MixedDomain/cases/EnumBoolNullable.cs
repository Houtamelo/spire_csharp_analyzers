//@ exhaustive
// (Direction, bool?) — enum x nullable bool cross-product
public class EnumBoolNullable
{
    public int Test(Direction d, bool? b) => (d, b) switch
    {
        (Direction.North, true) => 1,
        (Direction.North, false) => 2,
        (Direction.North, null) => 3,
        (Direction.South, _) => 4,
    };
}
