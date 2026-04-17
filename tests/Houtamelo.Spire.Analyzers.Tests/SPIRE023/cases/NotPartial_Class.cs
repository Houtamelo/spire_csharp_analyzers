//@ should_fail
// SPIRE023: containing class is not partial

public class Host_023_NotPartial
{
    public static void Call([Inlinable] Action<int> action, int x) //~ ERROR
    {
        action(x);
    }
}
