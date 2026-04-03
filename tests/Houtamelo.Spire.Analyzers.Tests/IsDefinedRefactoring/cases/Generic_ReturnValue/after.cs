public class Test
{
    public bool Method(Status v)
    {
        return SpireEnum<Status>.TryFrom(v, out var result);
    }
}
