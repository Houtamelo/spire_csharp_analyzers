//@ should_pass

public partial class Host_024_Action8
{
    public static void Call(
        [Inlinable] Action<int, int, int, int, int, int, int, int> action,
        int x)
    {
        action(x, x, x, x, x, x, x, x);
    }
}
