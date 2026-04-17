//@ should_fail
// SPIRE025: [Inlinable] ref parameter

public partial class Host_025_Ref
{
    public static void Call([Inlinable] ref Action<int> action, int x) //~ ERROR
    {
        action(x);
    }
}
