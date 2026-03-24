//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array of a [MustBeInit] enum that has a zero-valued member.
public class NoReport_EnumArray_WithZeroMember
{
    void M()
    {
        var arr = new MustInitEnumWithZero[5];
    }
}
