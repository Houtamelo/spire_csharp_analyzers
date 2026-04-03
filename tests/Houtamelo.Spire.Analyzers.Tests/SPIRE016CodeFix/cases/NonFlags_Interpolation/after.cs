public class Test
{
    public void Method(int v)
    {
        string s = $"status={SpireEnum<StatusNoZero>.From(v)}";
    }
}
