//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [EnforceInitialization] record struct.
class Detect_RecordStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationRecordStruct)); //~ ERROR
    }
}
