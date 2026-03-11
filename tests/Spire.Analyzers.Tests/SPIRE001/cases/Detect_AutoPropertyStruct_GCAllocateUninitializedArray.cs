//@ should_fail
// Ensure that SPIRE001 IS triggered when using GC.AllocateUninitializedArray with a [MustBeInit] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_GCAllocateUninitializedArray
{
    public void Method()
    {
        var arr = System.GC.AllocateUninitializedArray<MustInitStructWithAutoProperty>(5); //~ ERROR
    }
}
