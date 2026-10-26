//@ exhaustive
// Traditional constant-pattern arms retain the exact high-value singleton between relational ranges.
public class LongTraditionalConstantPartition
{
    public int Test(long value) => value switch
    {
        < 9007199254740992L => 0,
        9007199254740992L => 1,
        9007199254740993L => 2,
        9007199254740994L => 3,
        > 9007199254740994L => 4,
    };
}
