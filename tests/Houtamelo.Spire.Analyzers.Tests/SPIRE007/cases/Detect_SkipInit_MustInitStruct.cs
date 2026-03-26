//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called with a EnforceInitializationStruct.
public class Detect_SkipInit_EnforceInitializationStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out EnforceInitializationStruct s); //~ ERROR
    }
}
