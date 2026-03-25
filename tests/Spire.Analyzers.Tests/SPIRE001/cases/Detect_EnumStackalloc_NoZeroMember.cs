//@ should_fail
// Ensure that SPIRE001 IS triggered when stackalloc of MustInitEnumNoZero produces unnamed default(0) elements.
public class Detect_EnumStackalloc_NoZeroMember
{
    void M()
    {
        Span<MustInitEnumNoZero> span = stackalloc MustInitEnumNoZero[5]; //~ ERROR
    }
}
