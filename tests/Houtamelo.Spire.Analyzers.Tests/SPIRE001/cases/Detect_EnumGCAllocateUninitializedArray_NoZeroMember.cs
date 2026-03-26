//@ should_fail
// Ensure that SPIRE001 IS triggered when GC.AllocateUninitializedArray<EnforceInitializationEnumNoZero> produces unnamed default(0) elements.
public class Detect_EnumGCAllocateUninitializedArray_NoZeroMember
{
    void M()
    {
        var arr = GC.AllocateUninitializedArray<EnforceInitializationEnumNoZero>(5); //~ ERROR
    }
}
