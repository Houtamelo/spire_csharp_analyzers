//@ should_fail
// SPIRE023: inner partial is fine but the enclosing type is not

public class Outer_023
{
    public partial class Inner
    {
        public static void Call([Inlinable] Action<int> action, int x) //~ ERROR
        {
            action(x);
        }
    }
}
