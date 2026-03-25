//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called with a EnforceInitializationReadonlyStruct.
public class Detect_SkipInit_ReadonlyStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out EnforceInitializationReadonlyStruct s); //~ ERROR
    }
}
