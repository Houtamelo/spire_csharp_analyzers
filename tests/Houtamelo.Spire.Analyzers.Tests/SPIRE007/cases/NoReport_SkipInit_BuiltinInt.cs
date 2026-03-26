//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called with an int.
public class NoReport_SkipInit_BuiltinInt
{
    public void Method()
    {
        Unsafe.SkipInit(out int x);
    }
}
