//@ should_fail
// SPIRE022: [Inlinable] on a non-Action/Func user-defined delegate

public partial class Host_022_Custom
{
    public static void Call([Inlinable] MyDelegate d, int x) //~ ERROR
    {
        d(x);
    }
}
