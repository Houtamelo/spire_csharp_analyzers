//@ should_fail
// Ensure that SPIRE001 IS triggered when using GC.AllocateUninitializedArray with a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_GCAllocateUninitializedArray
{
    public void Method()
    {
        var arr = System.GC.AllocateUninitializedArray<EnforceInitializationStructWithAutoProperty>(5); //~ ERROR
    }
}
