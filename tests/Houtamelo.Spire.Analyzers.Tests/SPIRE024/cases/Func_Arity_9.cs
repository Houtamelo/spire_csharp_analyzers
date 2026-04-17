//@ should_fail
// SPIRE024: Func<T1..T9, TR> has arity 9 (9 inputs), exceeds max 8

public partial class Host_024_Func9
{
    public static int Call(
        [Inlinable] Func<int, int, int, int, int, int, int, int, int, int> f, //~ ERROR
        int x)
    {
        return f(x, x, x, x, x, x, x, x, x);
    }
}
