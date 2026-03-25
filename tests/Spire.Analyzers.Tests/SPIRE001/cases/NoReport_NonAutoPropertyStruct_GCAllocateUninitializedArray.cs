//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using GC.AllocateUninitializedArray with a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_GCAllocateUninitializedArray
{
    public void Method()
    {
        var arr = System.GC.AllocateUninitializedArray<EnforceInitializationStructWithNonAutoProperty>(5);
    }
}
