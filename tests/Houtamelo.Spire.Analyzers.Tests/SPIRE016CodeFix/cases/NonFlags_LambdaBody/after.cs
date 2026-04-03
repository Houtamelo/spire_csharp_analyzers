using System;

public class Test
{
    public void Method(int v)
    {
        Func<StatusNoZero> f = () => SpireEnum<StatusNoZero>.From(v);
    }
}
