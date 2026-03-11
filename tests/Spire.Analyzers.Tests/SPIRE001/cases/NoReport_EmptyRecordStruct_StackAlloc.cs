//@ should_pass
// Ensure that SPIRE001 is NOT triggered when stackalloc-ing an array of a [MustBeInit] record struct with no fields.
public class NoReport_EmptyRecordStruct_StackAlloc
{
    public void Method()
    {
        System.Span<EmptyMustInitRecordStruct> span = stackalloc EmptyMustInitRecordStruct[5];
    }
}
