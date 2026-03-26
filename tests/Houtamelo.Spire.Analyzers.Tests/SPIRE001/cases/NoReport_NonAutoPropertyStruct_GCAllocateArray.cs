//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using GC.AllocateArray with a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_GCAllocateArray
{
    public void Method()
    {
        var arr = System.GC.AllocateArray<EnforceInitializationStructWithNonAutoProperty>(5);
    }
}
