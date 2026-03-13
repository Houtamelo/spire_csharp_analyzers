//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called with a fieldless [MustBeInit] struct.
public class NoReport_SkipInit_FieldlessStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out EmptyMustInitStruct s);
    }
}
