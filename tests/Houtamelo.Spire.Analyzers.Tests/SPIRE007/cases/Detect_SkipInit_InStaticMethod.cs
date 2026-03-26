//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit appears in a static method.
public class Detect_SkipInit_InStaticMethod
{
    public static void Method()
    {
        Unsafe.SkipInit(out EnforceInitializationStruct s); //~ ERROR
    }
}
