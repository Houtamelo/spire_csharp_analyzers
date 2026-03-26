//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear is called with a Nullable<EnforceInitializationStruct>[] — element type is Nullable<T>, not [EnforceInitialization] itself.
public class NoReport_ArrayClear1_NullableEnforceInitialization
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct?[5];
        Array.Clear(arr);
    }
}
