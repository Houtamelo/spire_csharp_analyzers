//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a fixed-size array of a [MustBeInit] enum with no zero-valued member.
public class Detect_EnumArray_NoZeroMember
{
    void M()
    {
        var arr = new MustInitEnumNoZero[5]; //~ ERROR
    }
}
