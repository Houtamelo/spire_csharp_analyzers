//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called with a reference type.
public class NoReport_SkipInit_ReferenceType
{
    public void Method()
    {
        Unsafe.SkipInit(out string s);
    }
}
