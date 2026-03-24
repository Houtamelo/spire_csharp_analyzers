//@ should_fail
// Ensure that SPIRE003 IS triggered when using the default literal for a [MustBeInit] enum with no zero-valued member.
public class Detect_EnumDefault_NoZeroMember
{
    void M()
    {
        MustInitEnumNoZero x = default; //~ ERROR
    }
}
