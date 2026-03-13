//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is a built-in value type.
public class NoReport_BuiltinType_Int
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(int));
    }
}
