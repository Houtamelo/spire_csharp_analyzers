//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [MustBeInit] readonly record struct.
[MustBeInit]
readonly record struct ReadonlyRecordMustInit(int Value);

class Detect_ReadonlyRecordStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(ReadonlyRecordMustInit)); //~ ERROR
    }
}
