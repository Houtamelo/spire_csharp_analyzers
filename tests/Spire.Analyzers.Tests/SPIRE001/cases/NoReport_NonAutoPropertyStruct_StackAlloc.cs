//@ should_pass
// Ensure that SPIRE001 is NOT triggered when stackalloc-ing an array of a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_StackAlloc
{
    public void Method()
    {
        System.Span<EnforceInitializationStructWithNonAutoProperty> span = stackalloc EnforceInitializationStructWithNonAutoProperty[5];
    }
}
