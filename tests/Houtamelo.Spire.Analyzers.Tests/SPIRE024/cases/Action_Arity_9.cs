//@ should_fail
// SPIRE024: Action<T1..T9> has arity 9, exceeds max 8

public partial class Host_024_Action9
{
    public static void Call(
        [Inlinable] Action<int, int, int, int, int, int, int, int, int> action, //~ ERROR
        int x)
    {
        action(x, x, x, x, x, x, x, x, x);
    }
}
