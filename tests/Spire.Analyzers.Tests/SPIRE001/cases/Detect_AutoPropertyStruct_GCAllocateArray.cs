//@ should_fail
// Ensure that SPIRE001 IS triggered when using GC.AllocateArray with a [MustBeInit] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_GCAllocateArray
{
    public void Method()
    {
        var arr = System.GC.AllocateArray<MustInitStructWithAutoProperty>(5); //~ ERROR
    }
}
