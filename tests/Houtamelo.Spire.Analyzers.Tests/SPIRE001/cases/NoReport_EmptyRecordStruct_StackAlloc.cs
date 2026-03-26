//@ should_pass
// Ensure that SPIRE001 is NOT triggered when stackalloc-ing an array of a [EnforceInitialization] record struct with no fields.
public class NoReport_EmptyRecordStruct_StackAlloc
{
    public void Method()
    {
        System.Span<EmptyEnforceInitializationRecordStruct> span = stackalloc EmptyEnforceInitializationRecordStruct[5];
    }
}
