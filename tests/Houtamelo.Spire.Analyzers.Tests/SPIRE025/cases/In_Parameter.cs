//@ should_fail
// SPIRE025: [Inlinable] in parameter

public partial class Host_025_In
{
    public static void Call([Inlinable] in Action<int> action, int x) //~ ERROR
    {
        action(x);
    }
}
