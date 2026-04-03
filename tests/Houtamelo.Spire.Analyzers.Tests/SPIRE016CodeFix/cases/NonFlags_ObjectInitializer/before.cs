public class Container
{
    public StatusNoZero Status { get; set; }
}

public class Test
{
    public void Method(int v)
    {
        var c = new Container { Status = (StatusNoZero)v };
    }
}
