//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called with a plain struct.
public class NoReport_SkipInit_PlainStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out PlainStruct s);
    }
}
