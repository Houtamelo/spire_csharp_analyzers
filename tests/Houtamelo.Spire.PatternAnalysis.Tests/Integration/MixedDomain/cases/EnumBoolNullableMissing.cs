//@ not_exhaustive
// (Direction, bool?) — missing North+null
public class EnumBoolNullableMissing
{
    public int Test(Direction d, bool? b) => (d, b) switch
    {
        (Direction.North, true) => 1,
        (Direction.North, false) => 2,
        (Direction.South, _) => 4,
        //~ (Direction.North, null)
    };
}
