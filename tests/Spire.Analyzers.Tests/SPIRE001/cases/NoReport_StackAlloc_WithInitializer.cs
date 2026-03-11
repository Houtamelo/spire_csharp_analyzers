//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using stackalloc with an explicit initializer.
public class NoReport_StackAlloc_WithInitializer
{
    public void Method()
    {
        Span<MustInitStruct> span = stackalloc MustInitStruct[] { new MustInitStruct(1), new MustInitStruct(2) };
    }
}
