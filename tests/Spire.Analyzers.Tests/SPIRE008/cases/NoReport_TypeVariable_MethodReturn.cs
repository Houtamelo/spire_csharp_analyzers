//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the Type argument comes from a method return value.
public class NoReport_TypeVariable_MethodReturn
{
    void Method(object obj)
    {
        var result = RuntimeHelpers.GetUninitializedObject(obj.GetType());
    }
}
