//@ should_pass

public partial class Host_021_Direct
{
    public static void Call([Inlinable] Action<int> action, int x)
    {
        action(x);
    }
}
