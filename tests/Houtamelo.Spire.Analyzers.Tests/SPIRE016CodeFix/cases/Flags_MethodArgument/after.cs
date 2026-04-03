public class Test
{
    public void Method(int v)
    {
        Accept(SpireEnum<FlagsNoZero>.FromFlags(v));
    }

    private void Accept(FlagsNoZero f) { }
}
