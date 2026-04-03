public class Test
{
    public void Method(Status v)
    {
        if (SpireEnum<Status>.TryFrom(v, out var result))
        {
        }
    }
}
