public class Test
{
    public void Method(PlainEnum p)
    {
        StatusNoZero s = SpireEnum<StatusNoZero>.From((int)p);
    }
}
