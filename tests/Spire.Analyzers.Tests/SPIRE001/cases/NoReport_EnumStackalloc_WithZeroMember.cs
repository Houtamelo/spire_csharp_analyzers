//@ should_pass
// Ensure that SPIRE001 is NOT triggered when stackalloc of MustInitEnumWithZero produces default(0) = None, which is a named member.
public class NoReport_EnumStackalloc_WithZeroMember
{
    void M()
    {
        Span<MustInitEnumWithZero> span = stackalloc MustInitEnumWithZero[5];
    }
}
