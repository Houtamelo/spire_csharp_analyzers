//@ should_fail
// Ensure that SPIRE008 IS triggered when RuntimeHelpers.GetUninitializedObject is called on a [MustBeInit] enum with no zero-valued member.
public class Detect_EnumGetUninitializedObject_NoZero
{
    void M()
    {
        var result = RuntimeHelpers.GetUninitializedObject(typeof(MustInitEnumNoZero)); //~ ERROR
    }
}
