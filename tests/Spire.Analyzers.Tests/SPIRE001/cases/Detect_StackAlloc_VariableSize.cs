//@ should_fail
// Ensure that SPIRE001 IS triggered when using stackalloc with a variable size.
public class Detect_StackAlloc_VariableSize
{
    public void Method(int n)
    {
        Span<MustInitStruct> span = stackalloc MustInitStruct[n]; //~ ERROR
    }
}
