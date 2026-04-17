//@ should_pass

public partial class Host_022_Action
{
    public static void Call([Inlinable] Action<int> action, int x)
    {
        action(x);
    }
}
