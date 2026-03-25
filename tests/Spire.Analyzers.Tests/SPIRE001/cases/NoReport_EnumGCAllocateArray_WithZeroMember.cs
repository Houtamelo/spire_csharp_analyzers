//@ should_pass
// Ensure that SPIRE001 is NOT triggered when GC.AllocateArray<MustInitEnumWithZero> produces default(0) = None, which is a named member.
public class NoReport_EnumGCAllocateArray_WithZeroMember
{
    void M()
    {
        var arr = GC.AllocateArray<MustInitEnumWithZero>(5);
    }
}
