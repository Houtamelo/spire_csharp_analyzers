//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using stackalloc with size zero.
public class NoReport_StackAlloc_ZeroSize
{
    public void Method()
    {
        Span<EnforceInitializationStruct> span = stackalloc EnforceInitializationStruct[0];
    }
}
