//@ should_fail
// Ensure that SPIRE008 IS triggered when RuntimeHelpers.GetUninitializedObject is called on a [EnforceInitialization] enum with no zero-valued member.
public class Detect_EnumGetUninitializedObject_NoZero
{
    void M()
    {
        var result = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationEnumNoZero)); //~ ERROR
    }
}
