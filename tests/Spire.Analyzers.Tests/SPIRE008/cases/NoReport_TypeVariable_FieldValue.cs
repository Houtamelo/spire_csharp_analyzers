//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the Type argument is a field value (not a typeof literal).
public class NoReport_TypeVariable_FieldValue
{
    private Type _type = typeof(EnforceInitializationStruct);

    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(_type);
    }
}
