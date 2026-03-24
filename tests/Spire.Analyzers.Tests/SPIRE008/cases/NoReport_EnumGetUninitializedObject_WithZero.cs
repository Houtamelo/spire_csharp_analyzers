//@ should_pass
// Ensure that SPIRE008 is NOT triggered when RuntimeHelpers.GetUninitializedObject is called on a [MustBeInit] enum that has a zero-valued member (None=0).
public class NoReport_EnumGetUninitializedObject_WithZero
{
    void M()
    {
        var result = RuntimeHelpers.GetUninitializedObject(typeof(MustInitEnumWithZero));
    }
}
