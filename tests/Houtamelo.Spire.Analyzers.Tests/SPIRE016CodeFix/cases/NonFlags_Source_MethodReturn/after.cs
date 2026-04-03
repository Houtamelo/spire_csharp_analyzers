public class Test
{
    public void Method()
    {
        StatusNoZero s = SpireEnum<StatusNoZero>.From(GetValue());
    }

    private int GetValue() => 1;
}
