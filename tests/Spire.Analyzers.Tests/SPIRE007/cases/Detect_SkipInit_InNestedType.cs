//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit appears in a nested type.
public class Detect_SkipInit_InNestedType
{
    private class Inner
    {
        public void Method()
        {
            Unsafe.SkipInit(out MustInitStruct s); //~ ERROR
        }
    }
}
