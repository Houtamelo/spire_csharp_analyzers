//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the Type argument is a local variable (not a typeof literal directly in the call).
public class NoReport_TypeVariable_LocalVar
{
    void Method()
    {
        Type t = typeof(MustInitStruct);
        var obj = RuntimeHelpers.GetUninitializedObject(t);
    }
}
