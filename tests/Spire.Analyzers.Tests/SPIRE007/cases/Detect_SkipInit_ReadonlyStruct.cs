//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called with a MustInitReadonlyStruct.
public class Detect_SkipInit_ReadonlyStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out MustInitReadonlyStruct s); //~ ERROR
    }
}
