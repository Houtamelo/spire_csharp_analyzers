//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using GC.AllocateUninitializedArray with a [MustBeInit] struct with no fields.
public class NoReport_EmptyStruct_GCAllocateUninitializedArray
{
    public void Method()
    {
        var arr = System.GC.AllocateUninitializedArray<EmptyMustInitStruct>(5);
    }
}
