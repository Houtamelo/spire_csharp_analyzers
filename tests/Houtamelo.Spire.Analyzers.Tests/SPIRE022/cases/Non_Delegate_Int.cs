//@ should_fail
// SPIRE022: [Inlinable] on an int parameter

public partial class Host_022_Int
{
    public static void Call([Inlinable] int x) //~ ERROR
    {
        _ = x;
    }
}
