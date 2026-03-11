//@ should_fail
// Ensure that SPIRE001 IS triggered when using stackalloc with a constant size.
public class Detect_StackAlloc_ConstantSize
{
    public void Method()
    {
        Span<MustInitStruct> span = stackalloc MustInitStruct[5]; //~ ERROR
    }
}
