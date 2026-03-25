//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [EnforceInitialization] ref struct.
[EnforceInitialization]
ref struct RefEnforceInitialization
{
    public int Value;
    public RefEnforceInitialization(int v) { Value = v; }
}

class Detect_RefStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(RefEnforceInitialization)); //~ ERROR
    }
}
