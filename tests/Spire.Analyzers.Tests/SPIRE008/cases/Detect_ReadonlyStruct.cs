//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [MustBeInit] readonly struct.
class Detect_ReadonlyStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(MustInitReadonlyStruct)); //~ ERROR
    }
}
