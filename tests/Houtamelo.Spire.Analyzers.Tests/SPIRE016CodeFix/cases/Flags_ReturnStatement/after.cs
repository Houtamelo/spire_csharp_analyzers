public class Test
{
    public FlagsNoZero Method(int v)
    {
        return SpireEnum<FlagsNoZero>.FromFlags(v);
    }
}
