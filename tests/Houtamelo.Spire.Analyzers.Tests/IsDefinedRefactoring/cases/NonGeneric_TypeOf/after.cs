public class Test
{
    public void Method(int v)
    {
        if (SpireEnum<Status>.TryFrom(v, out var result))
        {
        }
    }
}
