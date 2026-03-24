//@ should_fail
// Ensure that SPIRE004 IS triggered when using new T() on a [MustBeInit] enum with no zero-valued member.
public class Detect_EnumNew_NoZeroMember
{
    void M()
    {
        var x = new MustInitEnumNoZero(); //~ ERROR
    }
}
