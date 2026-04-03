public class Test
{
    public void Method(int v)
    {
        Accept(SpireEnum<StatusNoZero>.From(v));
    }

    private void Accept(StatusNoZero s) { }
}
