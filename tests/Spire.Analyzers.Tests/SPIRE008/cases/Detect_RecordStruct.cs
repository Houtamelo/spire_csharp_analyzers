//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [MustBeInit] record struct.
class Detect_RecordStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(MustInitRecordStruct)); //~ ERROR
    }
}
