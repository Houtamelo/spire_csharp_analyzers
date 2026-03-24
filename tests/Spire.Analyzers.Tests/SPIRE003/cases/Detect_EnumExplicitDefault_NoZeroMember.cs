//@ should_fail
// Ensure that SPIRE003 IS triggered when using default(T) explicitly for a [MustBeInit] enum with no zero-valued member.
public class Detect_EnumExplicitDefault_NoZeroMember
{
    void M()
    {
        var x = default(MustInitEnumNoZero); //~ ERROR
    }
}
