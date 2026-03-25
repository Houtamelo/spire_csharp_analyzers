//@ should_fail
// Ensure that SPIRE001 IS triggered when GC.AllocateArray<MustInitEnumNoZero> produces unnamed default(0) elements.
public class Detect_EnumGCAllocateArray_NoZeroMember
{
    void M()
    {
        var arr = GC.AllocateArray<MustInitEnumNoZero>(5); //~ ERROR
    }
}
