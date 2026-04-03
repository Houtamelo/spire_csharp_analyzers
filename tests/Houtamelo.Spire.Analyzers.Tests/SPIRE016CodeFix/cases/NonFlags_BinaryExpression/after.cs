public class Test
{
    public void Method(int v)
    {
        bool isActive = SpireEnum<StatusNoZero>.From(v) == StatusNoZero.Active;
    }
}
