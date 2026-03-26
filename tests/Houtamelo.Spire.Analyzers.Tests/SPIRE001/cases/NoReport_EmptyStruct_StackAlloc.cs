//@ should_pass
// Ensure that SPIRE001 is NOT triggered when stackalloc-ing an array of a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_StackAlloc
{
    public void Method()
    {
        System.Span<EmptyEnforceInitializationStruct> span = stackalloc EmptyEnforceInitializationStruct[5];
    }
}
