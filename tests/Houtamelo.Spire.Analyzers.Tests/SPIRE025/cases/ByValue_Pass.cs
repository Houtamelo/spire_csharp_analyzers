//@ should_pass

public partial class Host_025_ByValue
{
    public static void Call([Inlinable] Action<int> action, int x)
    {
        action(x);
    }
}
