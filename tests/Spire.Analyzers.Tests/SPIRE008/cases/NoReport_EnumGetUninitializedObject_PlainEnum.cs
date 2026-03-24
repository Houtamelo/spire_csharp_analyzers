//@ should_pass
// Ensure that SPIRE008 is NOT triggered when RuntimeHelpers.GetUninitializedObject is called on a plain enum (no [MustBeInit]).
public class NoReport_EnumGetUninitializedObject_PlainEnum
{
    void M()
    {
        var result = RuntimeHelpers.GetUninitializedObject(typeof(PlainEnum));
    }
}
