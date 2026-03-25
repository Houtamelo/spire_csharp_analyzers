//@ should_fail
// Ensure that SPIRE001 IS triggered when GC.AllocateUninitializedArray<MustInitEnumNoZero> produces unnamed default(0) elements.
public class Detect_EnumGCAllocateUninitializedArray_NoZeroMember
{
    void M()
    {
        var arr = GC.AllocateUninitializedArray<MustInitEnumNoZero>(5); //~ ERROR
    }
}
