using System;

public class Test
{
    public void Method(int v)
    {
        Func<StatusNoZero> f = () => (StatusNoZero)v;
    }
}
