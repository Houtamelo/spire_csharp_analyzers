//@ should_fail
// SPIRE021: parameter stored in a field

public partial class Host_021_Field
{
    private static Action<int>? _stored;

    public static void Call([Inlinable] Action<int> action, int x)
    {
        _stored = action; //~ ERROR
        action(x);
    }
}
