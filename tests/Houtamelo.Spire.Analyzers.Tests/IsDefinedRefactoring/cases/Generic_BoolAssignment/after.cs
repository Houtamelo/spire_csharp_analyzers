public class Test
{
    public void Method(Status v)
    {
        bool valid = SpireEnum<Status>.TryFrom(v, out var result);
    }
}
