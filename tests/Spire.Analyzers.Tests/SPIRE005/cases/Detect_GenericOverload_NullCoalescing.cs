//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is in a null-coalescing expression.
public class Detect_GenericOverload_NullCoalescing
{
    public void Method()
    {
        object? x = null;
        var y = x ?? (object)Activator.CreateInstance<MustInitStruct>(); //~ ERROR
    }
}
