//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called with Nullable<EnforceInitializationStruct>.
public class NoReport_SkipInit_NullableEnforceInitialization
{
    public void Method()
    {
        Unsafe.SkipInit(out EnforceInitializationStruct? s);
    }
}
