//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [EnforceInitialization] readonly record struct.
[EnforceInitialization]
readonly record struct ReadonlyRecordEnforceInitialization(int Value);

class Detect_ReadonlyRecordStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(ReadonlyRecordEnforceInitialization)); //~ ERROR
    }
}
