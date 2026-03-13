//@ should_fail
// Ensure that SPIRE008 IS triggered when target is a [MustBeInit] ref struct.
[MustBeInit]
ref struct RefMustInit
{
    public int Value;
    public RefMustInit(int v) { Value = v; }
}

class Detect_RefStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(RefMustInit)); //~ ERROR
    }
}
