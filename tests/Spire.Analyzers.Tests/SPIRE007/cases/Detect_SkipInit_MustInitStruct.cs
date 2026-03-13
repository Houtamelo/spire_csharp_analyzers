//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called with a MustInitStruct.
public class Detect_SkipInit_MustInitStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out MustInitStruct s); //~ ERROR
    }
}
