//@ should_fail
// SPIRE021: tracked alias is reassigned

public partial class Host_021_Reassigned
{
    public static void Call([Inlinable] Action<int> action, int x)
    {
        var a = action;
        a = null!; //~ ERROR
        a(x);
    }
}
