//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using stackalloc with an explicit initializer.
public class NoReport_StackAlloc_WithInitializer
{
    public void Method()
    {
        Span<EnforceInitializationStruct> span = stackalloc EnforceInitializationStruct[] { new EnforceInitializationStruct(1), new EnforceInitializationStruct(2) };
    }
}
