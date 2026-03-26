//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called in generic code where T is unresolved.
public class NoReport_SkipInit_GenericTypeParam
{
    public static void Method<T>()
    {
        Unsafe.SkipInit(out T value);
    }
}
