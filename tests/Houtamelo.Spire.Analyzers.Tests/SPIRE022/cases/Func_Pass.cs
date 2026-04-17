//@ should_pass

public partial class Host_022_Func
{
    public static int Call([Inlinable] Func<int, int> f, int x)
    {
        return f(x);
    }
}
