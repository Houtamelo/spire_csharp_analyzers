//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using GC.AllocateArray with a [MustBeInit] struct with no fields.
public class NoReport_EmptyStruct_GCAllocateArray
{
    public void Method()
    {
        var arr = System.GC.AllocateArray<EmptyMustInitStruct>(5);
    }
}
