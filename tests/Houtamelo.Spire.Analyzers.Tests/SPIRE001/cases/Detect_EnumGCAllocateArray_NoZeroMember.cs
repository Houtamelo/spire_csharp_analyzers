//@ should_fail
// Ensure that SPIRE001 IS triggered when GC.AllocateArray<EnforceInitializationEnumNoZero> produces unnamed default(0) elements.
public class Detect_EnumGCAllocateArray_NoZeroMember
{
    void M()
    {
        var arr = GC.AllocateArray<EnforceInitializationEnumNoZero>(5); //~ ERROR
    }
}
