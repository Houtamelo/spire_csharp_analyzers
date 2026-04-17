//@ should_pass

public partial class Host_023_Partial
{
    public static void Call([Inlinable] Action<int> action, int x)
    {
        action(x);
    }
}
